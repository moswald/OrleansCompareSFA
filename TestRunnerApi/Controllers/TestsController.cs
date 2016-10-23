namespace TestRunnerApi.Controllers
{
    using System;
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.Fabric;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Web.Http;
    using Actors.Interfaces;
    using GrainInterfaces;
    using Microsoft.ServiceFabric.Actors;
    using Microsoft.ServiceFabric.Actors.Client;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Blob;
    using Newtonsoft.Json;
    using Orleans;

    [RoutePrefix("api/tests")]
    public class TestsController : ApiController
    {
        const int NameLength = 32;
        const int MaxPets = 2;

        static readonly Random Rng = new Random();

        readonly string _testSetupBlobName;
        readonly CloudBlobContainer _blobContainer;

        public TestsController()
        {
            var appConfig = FabricRuntime.GetActivationContext().GetConfigurationPackageObject("Config");
            var azureStorageConnectionString = appConfig.Settings.Sections["Storage"].Parameters["AzureStorageConnectionString"].Value;
            var blobContainerName = appConfig.Settings.Sections["Storage"].Parameters["BlobStorageContainer"].Value;
            _testSetupBlobName = appConfig.Settings.Sections["Storage"].Parameters["TestSetupBlobName"].Value;

            var storageAccount = CloudStorageAccount.Parse(azureStorageConnectionString);
            var blobStorageClient = storageAccount.CreateCloudBlobClient();
            _blobContainer = blobStorageClient.GetContainerReference(blobContainerName);
        }

        [HttpGet]
        [Route("setup")]
        public async Task<IHttpActionResult> Setup(int count, int extraDataSize = 0)
        {
            await _blobContainer.DeleteIfExistsAsync().ConfigureAwait(false);
            await _blobContainer.CreateAsync().ConfigureAwait(false);
            await _blobContainer.SetPermissionsAsync(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Container }).ConfigureAwait(false);

            var testState = new TestState
            {
                Count = count,
                Ids = Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToImmutableArray(),
                Names = Enumerable.Range(0, count * 2).Select(_ => RandomString(NameLength)).ToImmutableArray(),
                PetIds = Enumerable.Range(0, count)
                    .Select(_ => Rng.Next(0, MaxPets + 1))
                    .Select(petCount => Enumerable.Range(0, petCount).Select(_ => Guid.NewGuid()).ToImmutableArray())
                    .ToImmutableArray(),
                ExtraDataSize = extraDataSize
            };

            var jsonValue = JsonConvert.SerializeObject(testState);

            var blob = _blobContainer.GetBlockBlobReference(_testSetupBlobName);
            await blob.UploadTextAsync(jsonValue).ConfigureAwait(false);

            return Ok();
        }

        [HttpGet]
        [Route("initialize/orleans")]
        public async Task<IHttpActionResult> InitializeOrleans()
        {
            var result = await Initialize(
                async (grainId, bestFriendId, firstName, lastName, petIds, extraDataSize) =>
                {
                    var grain = GrainClient.GrainFactory.GetGrain<IFriendlyGrain>(grainId);
                    var pets = petIds.Select(petId => GrainClient.GrainFactory.GetGrain<IPetGrain>(petId)).ToArray();

                    await Task.WhenAll(pets.Select(pet => pet.Initialize(grain, pet.GetPrimaryKey().ToString()))).ConfigureAwait(false);
                    await grain.Initialize(bestFriendId, firstName, lastName, pets, extraDataSize).ConfigureAwait(false);
                });

            return Ok(result);
        }

        [HttpGet]
        [Route("initialize/actors")]
        public async Task<IHttpActionResult> InitializeActors()
        {
            var result = await Initialize(
                async (actorId, bestFriendId, firstName, lastName, petIds, extraDataSize) =>
                {
                    var actor = ActorProxy.Create<IFriendlyActor>(new ActorId(actorId));
                    var pets = petIds.Select(petId => ActorProxy.Create<IPetActor>(new ActorId(petId))).ToArray();

                    await Task.WhenAll(pets.Select(pet => pet.Initialize(actor, pet.GetActorId().ToString()))).ConfigureAwait(false);
                    await actor.Initialize(new ActorId(bestFriendId), firstName, lastName, pets, extraDataSize);
                });

            return Ok(result);
        }

        [HttpGet]
        [Route("query/names/orleans")]
        public async Task<IHttpActionResult> QueryOrleansNames(int iterations, string separator = " ")
        {
            var results = await QueryNames(
                iterations,
                separator,
                guid =>
                {
                    var grain = GrainClient.GrainFactory.GetGrain<IFriendlyGrain>(guid);
                    return grain.GetFullName(separator);
                });

            return Ok(new { success = results.Item1, time = results.Item2 } );
        }

        [HttpGet]
        [Route("query/names/actors")]
        public async Task<IHttpActionResult> QueryActorsNames(int iterations, string separator = " ")
        {
            var results = await QueryNames(
                iterations,
                separator,
                guid =>
                {
                    var actor = ActorProxy.Create<IFriendlyActor>(new ActorId(guid));
                    return actor.GetFullName(separator);
                });

            return Ok(new { success = results.Item1, time = results.Item2 } );
        }

        async Task<TimeSpan> Initialize(Func<Guid, Guid, string, string, ImmutableArray<Guid>, int, Task> create)
        {
            var testState = await LoadTestState();

            var initializationCalls = new Task[testState.Count];

            var sw = Stopwatch.StartNew();

            for (var i = 0; i != testState.Count; ++i)
            {
                var bestFriend = i + 1 == testState.Count ? 0 : i + 1;

                initializationCalls[i] = create(testState.Ids[i], testState.Ids[bestFriend], testState.FirstName(i), testState.LastName(i), testState.PetIds[i], testState.ExtraDataSize);
            }

            await Task.WhenAll(initializationCalls);

            sw.Stop();

            return sw.Elapsed;
        }

        async Task<Tuple<bool, TimeSpan>> QueryNames(int iterations, string separator, Func<Guid, Task<string>> queryName)
        {
            var testState = await LoadTestState();

            var queryCalls = new Task<bool>[iterations * testState.Count];

            var sw = Stopwatch.StartNew();

            for (var iteration = 0; iteration != iterations; ++iteration)
            {
                for (var grainIndex = 0; grainIndex != testState.Count; ++grainIndex)
                {
                    queryCalls[iteration * testState.Count + grainIndex] = CompareNames(queryName(testState.Ids[grainIndex]), testState.FirstName(grainIndex) + separator + testState.LastName(grainIndex));
                }
            }

            var results = await Task.WhenAll(queryCalls);
            sw.Stop();

            return Tuple.Create(results.All(x => x), sw.Elapsed);
        }

        static async Task<bool> CompareNames(Task<string> remoteName, string expected) => await remoteName == expected;

        async Task<TestState> LoadTestState()
        {
            var blob = _blobContainer.GetBlockBlobReference(_testSetupBlobName);
            var jsonValue = await blob.DownloadTextAsync().ConfigureAwait(false);

            return JsonConvert.DeserializeObject<TestState>(jsonValue);
        }

        static string RandomString(int length) => new string(Enumerable.Range(0, length).Select(c => (char)Rng.Next('A', 'Z' + 1)).ToArray());

        class TestState
        {
            public int Count { get; set; }
            public ImmutableArray<Guid> Ids { get; set; }
            public ImmutableArray<string> Names { get; set; }
            public ImmutableArray<ImmutableArray<Guid>> PetIds { get; set; }
            public int ExtraDataSize { get; set; }

            public string FirstName(int index) => Names[index * 2];
            public string LastName(int index) => Names[index * 2 + 1];
        }
    }
}

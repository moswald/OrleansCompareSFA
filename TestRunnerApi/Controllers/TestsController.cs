﻿namespace TestRunnerApi.Controllers
{
    using System;
    using System.Collections.Generic;
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
                    var bestFriend = GrainClient.GrainFactory.GetGrain<IFriendlyGrain>(bestFriendId);
                    var pets = petIds.Select(petId => GrainClient.GrainFactory.GetGrain<IPetGrain>(petId)).ToImmutableArray();

                    await Task.WhenAll(pets.Select(pet => pet.Initialize(grain, pet.GetPrimaryKey().ToString()))).ConfigureAwait(false);
                    await grain.Initialize(bestFriend, firstName, lastName, pets, extraDataSize).ConfigureAwait(false);
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
                    var bestFriend = ActorProxy.Create<IFriendlyActor>(new ActorId(bestFriendId));
                    var pets = petIds.Select(petId => ActorProxy.Create<IPetActor>(new ActorId(petId))).ToImmutableArray();

                    await Task.WhenAll(pets.Select(pet => pet.Initialize(actor, pet.GetActorId().ToString()))).ConfigureAwait(false);
                    await actor.Initialize(bestFriend, firstName, lastName, pets, extraDataSize);
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

        [HttpGet]
        [Route("query/pets/orleans")]
        public async Task<IHttpActionResult> QueryOrleansPets(int iterations)
        {
            var results = await QueryPetNames(
                iterations,
                guid =>
                {
                    var grain = GrainClient.GrainFactory.GetGrain<IFriendlyGrain>(guid);
                    return grain.GetPetNames();
                });

            return Ok(new { success = results.Item1, time = results.Item2 });
        }

        [HttpGet]
        [Route("query/pets/actors")]
        public async Task<IHttpActionResult> QueryActorsPets(int iterations)
        {
            var results = await QueryPetNames(
                iterations,
                guid =>
                {
                    var actor = ActorProxy.Create<IFriendlyActor>(new ActorId(guid));
                    return actor.GetPetNames();
                });

            return Ok(new { success = results.Item1, time = results.Item2 });
        }

        [HttpGet]
        [Route("query/friends/orleans")]
        public async Task<IHttpActionResult> QueryOrleansFriends(int iterations, int depth = 3, string separator = ", ")
        {
            var results = await QueryFriendNames(
                iterations,
                depth,
                separator,
                guid =>
                {
                    var grain = GrainClient.GrainFactory.GetGrain<IFriendlyGrain>(guid);
                    return grain.GetFriendNames(separator, depth);
                });

            return Ok(new { success = results.Item1, time = results.Item2 });
        }

        [HttpGet]
        [Route("query/friends/actors")]
        public async Task<IHttpActionResult> QueryActorsFriends(int iterations, int depth = 3, string separator = ", ")
        {
            var results = await QueryFriendNames(
                iterations,
                depth,
                separator,
                guid =>
                {
                    var grain = ActorProxy.Create<IFriendlyActor>(new ActorId(guid));
                    return grain.GetFriendNames(separator, depth);
                });

            return Ok(new { success = results.Item1, time = results.Item2 });
        }

        async Task<TimeSpan> Initialize(Func<Guid, Guid, string, string, ImmutableArray<Guid>, int, Task> create)
        {
            var testState = await LoadTestState();

            var initializationCalls = new Task[testState.Count];

            var sw = Stopwatch.StartNew();

            for (var i = 0; i != testState.Count; ++i)
            {
                var friendIndex = testState.FriendIndex(i);
                initializationCalls[i] = create(testState.Ids[i], testState.Ids[friendIndex], testState.FirstName(i), testState.LastName(i), testState.PetIds[i], testState.ExtraDataSize);
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
                for (var index = 0; index != testState.Count; ++index)
                {
                    queryCalls[iteration * testState.Count + index] = CompareNames(queryName(testState.Ids[index]), testState.FirstName(index) + separator + testState.LastName(index));
                }
            }

            var results = await Task.WhenAll(queryCalls);
            sw.Stop();

            return Tuple.Create(results.All(x => x), sw.Elapsed);
        }

        async Task<Tuple<bool, TimeSpan>> QueryPetNames(int iterations, Func<Guid, Task<IEnumerable<string>>> queryNames)
        {
            var testState = await LoadTestState();

            var queryCalls = new Task<bool>[iterations * testState.Count];

            var sw = Stopwatch.StartNew();

            for (var iteration = 0; iteration != iterations; ++iteration)
            {
                for (var grainIndex = 0; grainIndex != testState.Count; ++grainIndex)
                {
                    queryCalls[iteration * testState.Count + grainIndex] = CompareNames(queryNames(testState.Ids[grainIndex]), testState.PetNames(grainIndex));
                }
            }

            var results = await Task.WhenAll(queryCalls);
            sw.Stop();

            return Tuple.Create(results.All(x => x), sw.Elapsed);
        }

        async Task<Tuple<bool, TimeSpan>> QueryFriendNames(int iterations, int depth, string separator, Func<Guid, Task<string>> queryNames)
        {
            var testState = await LoadTestState();

            var queryCalls = new Task<bool>[iterations * testState.Count];

            var sw = Stopwatch.StartNew();

            for (var iteration = 0; iteration != iterations; ++iteration)
            {
                for (var index = 0; index < testState.Count; index += depth + 1)
                {
                    var friendNames = testState.FriendNames(index, depth);

                    queryCalls[iteration * testState.Count + index] =
                        CompareNames(
                            queryNames(testState.Ids[index]),
                            string.Join(separator, friendNames));
                }
            }

            var results = await Task.WhenAll(queryCalls.Where(t => t != null));
            sw.Stop();

            return Tuple.Create(results.All(x => x), sw.Elapsed);
        }

        static async Task<bool> CompareNames(Task<string> remoteName, string expected)
        {
            var remote = await remoteName;
            return remote == expected;
        }

        static async Task<bool> CompareNames(Task<IEnumerable<string>> remotePetNames, IEnumerable<string> expectedNames) => !(await remotePetNames).Except(expectedNames).Any();

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
            public IEnumerable<string> PetNames(int index) => PetIds[index].Any() ? PetIds[index].Select(g => g.ToString()).ToArray() : Array.Empty<string>();
            public int FriendIndex(int index) => index + 1 == Count ? 0 : index + 1;

            public IEnumerable<string> FriendNames(int index, int count)
            {
                var names = new List<string>();

                for (var i = 0; i != count + 1; ++i)
                {
                    names.Add(FirstName(index));
                    index = FriendIndex(index);
                }

                return names;
            }
        }
    }
}

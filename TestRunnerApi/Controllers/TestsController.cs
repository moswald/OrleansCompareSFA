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
    using TestModels;

    [RoutePrefix("api/tests")]
    public class TestsController : ApiController
    {
        const int NameLength = 32;
        const int MaxPets = 2;

        static readonly Random Rng = new Random();

        readonly string _testSetupBlobName;
        readonly CloudBlobContainer _blobContainer;
        readonly TestState _testState;

        public TestsController()
        {
            var appConfig = FabricRuntime.GetActivationContext().GetConfigurationPackageObject("Config");
            var azureStorageConnectionString = appConfig.Settings.Sections["Storage"].Parameters["AzureStorageConnectionString"].Value;
            var blobContainerName = appConfig.Settings.Sections["Storage"].Parameters["BlobStorageContainer"].Value;
            _testSetupBlobName = appConfig.Settings.Sections["Storage"].Parameters["TestSetupBlobName"].Value;

            var storageAccount = CloudStorageAccount.Parse(azureStorageConnectionString);
            var blobStorageClient = storageAccount.CreateCloudBlobClient();
            _blobContainer = blobStorageClient.GetContainerReference(blobContainerName);

            var blob = _blobContainer.GetBlockBlobReference(_testSetupBlobName);
            if (blob.Exists())
            {
                var json = blob.DownloadText();
                _testState = JsonConvert.DeserializeObject<TestState>(json);
            }
        }

        [HttpGet]
        [Route("setup")]
        public async Task<IHttpActionResult> Setup(int count, int extraDataSize = 0, int calculatorTestSize = 100000)
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
                ExtraData = new byte[extraDataSize],
                CalculatorTestValues = Enumerable.Range(0, calculatorTestSize).Select(_ => Rng.NextDouble()).ToImmutableArray()
            };

            testState.ExpectedSum = testState.CalculatorTestValues.AsParallel().Sum();
            Rng.NextBytes(testState.ExtraData);

            await SaveTestState(testState);

            return Ok();
        }

        [HttpGet]
        [Route("initialize/orleans")]
        public async Task<IHttpActionResult> InitializeOrleans()
        {
            if (_testState == null)
                {
                return BadRequest("Test state was not initialized. Call the /api/tests/setup endpoint first.");
            }

            var test = new OrleansTests(_testState);
            return Ok(await test.Initialize().ConfigureAwait(false));
        }

        [HttpGet]
        [Route("initialize/actors")]
        public async Task<IHttpActionResult> InitializeActors()
        {
            if (_testState == null)
                {
                return BadRequest("Test state was not initialized. Call the /api/tests/setup endpoint first.");
            }

            var test = new ServiceFabricActorsTests(_testState);
            return Ok(await test.Initialize().ConfigureAwait(false));
        }

        [HttpGet]
        [Route("query/names/orleans")]
        public async Task<IHttpActionResult> QueryOrleansNames(int iterations, string separator = " ")
        {
            if (_testState == null)
                {
                return BadRequest("Test state was not initialized. Call the /api/tests/setup endpoint first.");
            }

            var test = new OrleansTests(_testState);
            return Ok(await test.QueryNames(iterations, separator).ConfigureAwait(false));
        }

        [HttpGet]
        [Route("query/names/actors")]
        public async Task<IHttpActionResult> QueryActorsNames(int iterations, string separator = " ")
        {
            if (_testState == null)
                {
                return BadRequest("Test state was not initialized. Call the /api/tests/setup endpoint first.");
            }

            var test = new ServiceFabricActorsTests(_testState);
            return Ok(await test.QueryNames(iterations, separator).ConfigureAwait(false));
        }

        [HttpGet]
        [Route("query/pets/orleans")]
        public async Task<IHttpActionResult> QueryOrleansPets(int iterations)
        {
            if (_testState == null)
                {
                return BadRequest("Test state was not initialized. Call the /api/tests/setup endpoint first.");
            }

            var test = new OrleansTests(_testState);
            return Ok(await test.QueryPetNames(iterations).ConfigureAwait(false));
        }

        [HttpGet]
        [Route("query/pets/actors")]
        public async Task<IHttpActionResult> QueryActorsPets(int iterations)
        {
            if (_testState == null)
                {
                return BadRequest("Test state was not initialized. Call the /api/tests/setup endpoint first.");
            }

            var test = new ServiceFabricActorsTests(_testState);
            return Ok(await test.QueryPetNames(iterations).ConfigureAwait(false));
        }

        [HttpGet]
        [Route("query/friends/orleans")]
        public async Task<IHttpActionResult> QueryOrleansFriends(int iterations, int depth = 3, string separator = ", ")
        {
            if (_testState == null)
                {
                return BadRequest("Test state was not initialized. Call the /api/tests/setup endpoint first.");
            }

            var test = new OrleansTests(_testState);
            return Ok(await test.QueryFriendNames(iterations, depth, separator).ConfigureAwait(false));
        }

        [HttpGet]
        [Route("query/friends/actors")]
        public async Task<IHttpActionResult> QueryActorsFriends(int iterations, int depth = 3, string separator = ", ")
        {
            if (_testState == null)
                {
                return BadRequest("Test state was not initialized. Call the /api/tests/setup endpoint first.");
            }

            var test = new ServiceFabricActorsTests(_testState);
            return Ok(await test.QueryFriendNames(iterations, depth, separator).ConfigureAwait(false));
        }

        [HttpGet]
        [Route("update/lastName/orleans")]
        public async Task<IHttpActionResult> UpdateOrleansLastName(int iterations)
        {
            if (_testState == null)
                {
                return BadRequest("Test state was not initialized. Call the /api/tests/setup endpoint first.");
            }

            var newNames = Enumerable.Range(0, iterations)
                .Select(_ => Enumerable.Range(0, _testState.Count)
                    .Select(__ => RandomString(NameLength)).ToImmutableArray())
                .ToImmutableArray();

            var test = new OrleansTests(_testState);
            return Ok(await test.UpdateNames(iterations, newNames).ConfigureAwait(false));
        }

        [HttpGet]
        [Route("update/lastName/actors")]
        public async Task<IHttpActionResult> UpdateActorsLastName(int iterations)
        {
            if (_testState == null)
                {
                return BadRequest("Test state was not initialized. Call the /api/tests/setup endpoint first.");
            }

            var newNames = Enumerable.Range(0, iterations)
                .Select(_ => Enumerable.Range(0, _testState.Count)
                    .Select(__ => RandomString(NameLength)).ToImmutableArray())
                .ToImmutableArray();

            var test = new ServiceFabricActorsTests(_testState);
            return Ok(await test.UpdateNames(iterations, newNames).ConfigureAwait(false));
        }

        [HttpGet]
        [Route("calculator/orleans")]
        public async Task<IHttpActionResult> OrleansCalculator()
        {
            if (_testState == null)
            {
                return BadRequest("Test state was not initialized. Call the /api/tests/setup endpoint first.");
            }

            var sw = Stopwatch.StartNew();

            var result = await _testState.CalculatorTestValues
                .AsParallel()
                .Aggregate(
                    Task.FromResult(0.0),
                    async (a, b) =>
                    {
                        var grain = GrainClient.GrainFactory.GetGrain<ICalculatorGrain>(0);
                        return await grain.Add(await a, b);
                    });

            sw.Stop();
            return Ok(new { success = Math.Abs(result - _testState.ExpectedSum) < 1E-09, time = sw.Elapsed });
        }

        [HttpGet]
        [Route("calculator/actors")]
        public async Task<IHttpActionResult> ActorsCalculator()
        {
            if (_testState == null)
            {
                return BadRequest("Test state was not initialized. Call the /api/tests/setup endpoint first.");
            }

            var sw = Stopwatch.StartNew();

            var result = await _testState.CalculatorTestValues
                .AsParallel()
                .Aggregate(
                    Task.FromResult(0.0),
                    async (a, b) =>
                    {
                        var actor = ActorProxy.Create<ICalculatorActor>(new ActorId(0));
                        return await actor.Add(await a, b);
                    });

            sw.Stop();
            return Ok(new { success = Math.Abs(result - _testState.ExpectedSum) < 1E-09, time = sw.Elapsed });
        }

        [HttpGet]
        [Route("full/orleans")]
        public async Task<IHttpActionResult> OrleansFull(int iterations = 10)
            {
            if (_testState == null)
                {
                return BadRequest("Test state was not initialized. Call the /api/tests/setup endpoint first.");
        }

            var newNames = Enumerable.Range(0, iterations)
                .Select(_ => Enumerable.Range(0, _testState.Count)
                    .Select(__ => RandomString(NameLength)).ToImmutableArray())
                .ToImmutableArray();

            var tests = new OrleansTests(_testState);

            var init = await tests.Initialize().ConfigureAwait(false);
            var queryNames = await tests.QueryNames(iterations, " ").ConfigureAwait(false);
            var queryPets = await tests.QueryPetNames(iterations).ConfigureAwait(false);
            var queryFriends = await tests.QueryFriendNames(iterations, 3, " ").ConfigureAwait(false);
            var updateNames = await tests.UpdateNames(iterations, newNames).ConfigureAwait(false);

            return Ok(new { init, queryNames, queryPets, queryFriends, updateNames });
                }

        [HttpGet]
        [Route("full/actors")]
        public async Task<IHttpActionResult> ActorsFull(int iterations = 10)
        {
            if (_testState == null)
            {
                return BadRequest("Test state was not initialized. Call the /api/tests/setup endpoint first.");
            }
            
            var newNames = Enumerable.Range(0, iterations)
                .Select(_ => Enumerable.Range(0, _testState.Count)
                    .Select(__ => RandomString(NameLength)).ToImmutableArray())
                .ToImmutableArray();

            var tests = new ServiceFabricActorsTests(_testState);

            var init = await tests.Initialize().ConfigureAwait(false);
            var queryNames = await tests.QueryNames(iterations, " ").ConfigureAwait(false);
            var queryPets = await tests.QueryPetNames(iterations).ConfigureAwait(false);
            var queryFriends = await tests.QueryFriendNames(iterations, 3, " ").ConfigureAwait(false);
            var updateNames = await tests.UpdateNames(iterations, newNames).ConfigureAwait(false);

            return Ok(new { init, queryNames, queryPets, queryFriends, updateNames });
        }

        async Task SaveTestState(TestState testState)
        {
            var jsonValue = JsonConvert.SerializeObject(testState);

            var blob = _blobContainer.GetBlockBlobReference(_testSetupBlobName);
            await blob.UploadTextAsync(jsonValue).ConfigureAwait(false);
        }

        static string RandomString(int length) => new string(Enumerable.Range(0, length).Select(c => (char)Rng.Next('A', 'Z' + 1)).ToArray());
            }
        }

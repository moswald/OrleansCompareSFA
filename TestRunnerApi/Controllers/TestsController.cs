namespace TestRunnerApi.Controllers
{
    using System;
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
    using Newtonsoft.Json.Linq;
    using Orleans;

    [RoutePrefix("api/tests")]
    public class TestsController : ApiController
    {
        const int NameLength = 32;

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

            var ids = Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToArray();
            var names = Enumerable.Range(0, count * 2).Select(_ => RandomString(NameLength)).ToArray();

            var jsonValue = JObject
                .FromObject(
                    new
                    {
                        count,
                        ids,
                        names,
                        extraDataSize
                    })
                .ToString(Formatting.Indented);

            var blob = _blobContainer.GetBlockBlobReference(_testSetupBlobName);
            await blob.UploadTextAsync(jsonValue).ConfigureAwait(false);

            return Ok();
        }

        [HttpGet]
        [Route("initializeOrleans/")]
        public async Task<IHttpActionResult> InitializeOrleans()
        {
            var result = await Initialize(
                (grainId, bestFriendId, firstName, lastName, extraDataSize) =>
                {
                    var grain = GrainClient.GrainFactory.GetGrain<IFriendlyGrain>(grainId);
                    return grain.Initialize(bestFriendId, firstName, lastName, extraDataSize);
                });

            return Ok(result);
        }

        [HttpGet]
        [Route("initializeActors/")]
        public async Task<IHttpActionResult> InitializeActors()
        {
            var result = await Initialize(
                (actorId, bestFriendId, firstName, lastName, extraDataSize) =>
                {
                    var actor = ActorProxy.Create<IFriendlyActor>(new ActorId(actorId));
                    return actor.Initialize(new ActorId(bestFriendId), firstName, lastName, extraDataSize);
                });

            return Ok(result);
        }

        async Task<TimeSpan> Initialize(Func<Guid, Guid, string, string, int, Task> create)
        {
            var blob = _blobContainer.GetBlockBlobReference(_testSetupBlobName);
            var jsonValue = await blob.DownloadTextAsync().ConfigureAwait(false);

            var json = JObject.Parse(jsonValue);

            var count = json["count"].Value<int>();
            var ids = json["ids"].Values<string>().Select(Guid.Parse).ToArray();
            var names = json["names"].Values<string>().ToArray();
            var extraDataSize = json["extraDataSize"].Value<int>();

            var initializationCalls = new Task[count];

            var sw = Stopwatch.StartNew();

            for (var i = 0; i != count; ++i)
            {
                var bestFriend = i + 1 == count ? 0 : i + 1;

                initializationCalls[i] = create(ids[i], ids[bestFriend], names[i * 2], names[i * 2 + 1], extraDataSize);
            }

            await Task.WhenAll(initializationCalls);

            sw.Stop();

            return sw.Elapsed;
        }

        static string RandomString(int length) => new string(Enumerable.Range(0, length).Select(c => (char)Rng.Next('A', 'Z' + 1)).ToArray());
    }
}

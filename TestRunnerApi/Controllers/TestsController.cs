namespace TestRunnerApi.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Web.Http;
    using Actors.Interfaces;
    using GrainInterfaces;
    using Microsoft.ServiceFabric.Actors;
    using Microsoft.ServiceFabric.Actors.Client;
    using Orleans;

    [RoutePrefix("api/tests")]
    public class TestsController : ApiController
    {
        const int GrainCount = 10000;
        const int NameLength = 32;

        static readonly Random Rng = new Random();

        [HttpGet]
        [Route("initializeOrleans/")]
        public async Task<IHttpActionResult> InitializeOrleans()
        {
            var grainIds = Enumerable.Range(0, GrainCount).Select(_ => Guid.NewGuid()).ToArray();
            var grainNames = Enumerable.Range(0, GrainCount * 2).Select(_ => RandomString(NameLength)).ToArray();

            var initializationCalls = new Task[GrainCount];

            var sw = Stopwatch.StartNew();

            for (var i = 0; i != GrainCount; ++i)
            {
                var grain = GrainClient.GrainFactory.GetGrain<IFriendlyGrain>(grainIds[i]);
                var bestFriend = i + 1 == GrainCount ? 0 : i + 1;

                initializationCalls[i] = grain.Initialize(grainIds[bestFriend], grainNames[i * 2], grainNames[i * 2 + 1], 0);
            }

            await Task.WhenAll(initializationCalls);

            sw.Stop();

            return Ok(sw.Elapsed);
        }

        [HttpGet]
        [Route("initializeActors/")]
        public async Task<IHttpActionResult> InitializeActors()
        {
            var actorIds = Enumerable.Range(0, GrainCount).Select(_ => ActorId.CreateRandom()).ToArray();
            var actorNames = Enumerable.Range(0, GrainCount * 2).Select(_ => RandomString(NameLength)).ToArray();

            var initializationCalls = new Task[GrainCount];

            var sw = Stopwatch.StartNew();

            for (var i = 0; i != GrainCount; ++i)
            {
                var grain = ActorProxy.Create<IFriendlyActor>(actorIds[i]);
                var bestFriend = i + 1 == GrainCount ? 0 : i + 1;

                initializationCalls[i] = grain.Initialize(actorIds[bestFriend], actorNames[i * 2], actorNames[i * 2 + 1], 0);
            }

            await Task.WhenAll(initializationCalls);

            sw.Stop();

            return Ok(sw.Elapsed);
        }

        static string RandomString(int length) => new string(Enumerable.Range(0, length).Select(c => (char)Rng.Next('A', 'Z' + 1)).ToArray());

        static IEnumerable<Tuple<T, T>> ToPairs<T>(IEnumerable<T> sequence)
        {
            using (var e = sequence.GetEnumerator())
            {
                while (e.MoveNext())
                {
                    var first = e.Current;
                    e.MoveNext();

                    yield return Tuple.Create(first, e.Current);
                }
            }
        }
    }
}

namespace TestRunnerApi.TestModels
{
    using System.Collections.Immutable;
    using System.Linq;
    using System.Threading.Tasks;
    using Actors.Interfaces;
    using Microsoft.ServiceFabric.Actors;
    using Microsoft.ServiceFabric.Actors.Client;

    class ServiceFabricActorsTests : VirtualActorTests
    {
        public ServiceFabricActorsTests(TestState testState)
            : base(testState)
        {
        }

        public async Task<TestResult> Initialize() =>
            await Initialize(
                async (actorId, bestFriendId, firstName, lastName, petIds, extraData) =>
                {
                    var actor = ActorProxy.Create<IFriendlyActor>(new ActorId(actorId));
                    var bestFriend = ActorProxy.Create<IFriendlyActor>(new ActorId(bestFriendId));
                    var pets = petIds.Select(petId => ActorProxy.Create<IPetActor>(new ActorId(petId))).ToImmutableArray();

                    await Task.WhenAll(pets.Select(pet => pet.Initialize(actor, pet.GetActorId().ToString()))).ConfigureAwait(false);
                    await actor.Initialize(bestFriend, firstName, lastName, pets, extraData);
                }).ConfigureAwait(false);

        public async Task<TestResult> QueryNames(int iterations, string separator) =>
            await QueryNames(
                iterations,
                separator,
                guid =>
                {
                    var actor = ActorProxy.Create<IFriendlyActor>(new ActorId(guid));
                    return actor.GetFullName(separator);
                }).ConfigureAwait(false);

        public async Task<TestResult> QueryPetNames(int iterations) =>
            await QueryPetNames(
                iterations,
                guid =>
                {
                    var actor = ActorProxy.Create<IFriendlyActor>(new ActorId(guid));
                    return actor.GetPetNames();
                }).ConfigureAwait(false);

        public async Task<TestResult> QueryFriendNames(int iterations, int depth, string separator) =>
            await QueryFriendNames(
                iterations,
                depth,
                separator,
                guid =>
                {
                    var actor = ActorProxy.Create<IFriendlyActor>(new ActorId(guid));
                    return actor.GetFriendNames(separator, depth);
                }).ConfigureAwait(false);

        public async Task<TestResult> UpdateNames(int iterations, ImmutableArray<ImmutableArray<string>> newNames) =>
            await UpdateNames(
                iterations,
                newNames,
                async (guid, name) =>
                {
                    var actor = ActorProxy.Create<IFriendlyActor>(new ActorId(guid));
                    await actor.UpdateLastName(name);
                    return await actor.GetLastName();
                }).ConfigureAwait(false);
    }
}

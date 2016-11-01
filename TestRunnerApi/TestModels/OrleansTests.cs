namespace TestRunnerApi.TestModels
{
    using System.Collections.Immutable;
    using System.Linq;
    using System.Threading.Tasks;
    using GrainInterfaces;
    using Orleans;
    using Orleans.Concurrency;

    class OrleansTests : VirtualActorTests
    {
        public OrleansTests(TestState testState)
            : base(testState)
        {
        }

        public async Task<TestResult> Initialize() =>
            await Initialize(
                async (grainId, bestFriendId, firstName, lastName, petIds, extraData) =>
                {
                    var grain = GrainClient.GrainFactory.GetGrain<IFriendlyGrain>(grainId);
                    var bestFriend = GrainClient.GrainFactory.GetGrain<IFriendlyGrain>(bestFriendId);
                    var pets = petIds.Select(petId => GrainClient.GrainFactory.GetGrain<IPetGrain>(petId)).ToImmutableArray();

                        await Task.WhenAll(pets.Select(pet => pet.Initialize(grain, pet.GetPrimaryKey().ToString()))).ConfigureAwait(false);
                        await grain.Initialize(bestFriend, firstName, lastName, pets, extraData.AsImmutable()).ConfigureAwait(false);
                }).ConfigureAwait(false);

        public async Task<TestResult> QueryNames(int iterations, string separator) =>
            await QueryNames(
                iterations,
                separator,
                guid =>
                {
                    var grain = GrainClient.GrainFactory.GetGrain<IFriendlyGrain>(guid);
                    return grain.GetFullName(separator);
                }).ConfigureAwait(false);

        public async Task<TestResult> QueryPetNames(int iterations) =>
            await QueryPetNames(
                iterations,
                guid =>
                {
                    var grain = GrainClient.GrainFactory.GetGrain<IFriendlyGrain>(guid);
                    return grain.GetPetNames();
                }).ConfigureAwait(false);

        public async Task<TestResult> QueryFriendNames(int iterations, int depth, string separator) =>
            await QueryFriendNames(
                iterations,
                depth,
                separator,
                guid =>
                {
                    var grain = GrainClient.GrainFactory.GetGrain<IFriendlyGrain>(guid);
                    return grain.GetFriendNames(separator, depth);
                }).ConfigureAwait(false);

        public async Task<TestResult> UpdateNames(int iterations, ImmutableArray<ImmutableArray<string>> newNames) =>
            await UpdateNames(
                iterations,
                newNames,
                async (guid, name) =>
                {
                    var grain = GrainClient.GrainFactory.GetGrain<IFriendlyGrain>(guid);
                    await grain.UpdateLastName(name);
                    return await grain.GetLastName();
                }).ConfigureAwait(false);
    }
}

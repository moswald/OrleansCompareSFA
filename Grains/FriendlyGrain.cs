namespace Grains
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Threading.Tasks;
    using GrainInterfaces;
    using Orleans;
    using Orleans.Concurrency;
    using Orleans.Runtime;
    using Orleans.Storage;

    class FriendlyGrainState
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public IFriendlyGrain BestFriend { get; set; }
        public IImmutableSet<IPetGrain> Pets { get; set; } = ImmutableHashSet<IPetGrain>.Empty;

        public byte[] ExtraData { get; set; }
    }

    class FriendlyGrain : Grain<FriendlyGrainState>, IFriendlyGrain
    {
        Task IFriendlyGrain.Initialize(IFriendlyGrain bestFriend, string firstName, string lastName, IList<IPetGrain> pets, Immutable<byte[]> extraData) =>
            SafeWriteStateAsync(
                () =>
            {
                    State.FirstName = firstName;
                    State.LastName = lastName;
                    State.BestFriend = bestFriend.Cast<IFriendlyGrain>();
                    State.Pets = pets.ToImmutableHashSet();
                    State.ExtraData = extraData.Value;
                });

        Task<string> IFriendlyGrain.GetFullName(string separator) => Task.FromResult(State.FirstName + separator + State.LastName);

        async Task<IEnumerable<string>> IFriendlyGrain.GetPetNames()
        {
            var names = State.Pets.Select(pet => pet.GetName());
            return (await Task.WhenAll(names)).ToImmutableArray();
        }

        async Task<string> IFriendlyGrain.GetFriendNames(string separator, int count)
        {
            if (count == 0)
            {
                return State.FirstName;
            }

            return State.FirstName + separator + await State.BestFriend.GetFriendNames(separator, --count);
        }

        Task<string> IFriendlyGrain.GetLastName() => Task.FromResult(State.LastName);

        Task IFriendlyGrain.UpdateLastName(string newName) => SafeWriteStateAsync(() => State.LastName = newName);

        async Task SafeWriteStateAsync(Action updateState)
        {
            var attempts = 0;
            do
            {
                updateState();
                var testValue = State.LastName;

                try
                {
                    await WriteStateAsync();
                    break;
                }
                catch (OrleansException)
                {
                    await ReadStateAsync();

                    if (State.LastName == testValue)
                    {
                        break;
                    }
                }
            } while (++attempts < 3);

            if (attempts == 3)
            {
                throw new InconsistentStateException($"After 3 attempts, could not write state for {this.GetPrimaryKey()}");
        }
    }
}
}

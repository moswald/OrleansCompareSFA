namespace Grains
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Threading.Tasks;
    using GrainInterfaces;
    using Orleans;

    class FriendlyGrainState
    {
        public Guid BestFriend { get; set; } = Guid.Empty;
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public IImmutableSet<IPetGrain> Pets { get; set; } = ImmutableHashSet<IPetGrain>.Empty;

        public byte[] ExtraData { get; set; }
    }

    class FriendlyGrain : Grain<FriendlyGrainState>, IFriendlyGrain
    {
        public Task Initialize(Guid bestFriend, string firstName, string lastName, IEnumerable<IPetGrain> pets, int extraDataSize)
        {
            State = new FriendlyGrainState
            {
                BestFriend = bestFriend,
                FirstName = firstName,
                LastName = lastName,
                Pets = pets.ToImmutableHashSet(),
                ExtraData = new byte[extraDataSize],
            };

            return WriteStateAsync();
        }

        public Task<string> GetFullName(string separator) => Task.FromResult(State.FirstName + separator + State.LastName);
    }
}

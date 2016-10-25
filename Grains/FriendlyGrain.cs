namespace Grains
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Threading.Tasks;
    using GrainInterfaces;
    using Orleans;

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
        Task IFriendlyGrain.Initialize(IFriendlyGrain bestFriend, string firstName, string lastName, IList<IPetGrain> pets, int extraDataSize)
        {
            State = new FriendlyGrainState
            {
                FirstName = firstName,
                LastName = lastName,
                BestFriend = bestFriend.Cast<IFriendlyGrain>(),
                Pets = pets.ToImmutableHashSet(),
                ExtraData = new byte[extraDataSize],
            };

            return WriteStateAsync();
        }

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
    }
}

namespace Grains
{
    using System;
    using System.Threading.Tasks;
    using GrainInterfaces;
    using Orleans;

    public class FriendlyGrainState
    {
        public Guid BestFriend { get; set; } = Guid.Empty;
        public string FirstName { get; set; }
        public string LastName { get; set; }

        public byte[] ExtraData { get; set; }
    }

    public class FriendlyGrain : Grain<FriendlyGrainState>, IFriendlyGrain
    {
        public Task Initialize(Guid bestFriend, string firstName, string lastName, int extraDataSize)
        {
            State = new FriendlyGrainState
            {
                BestFriend = bestFriend,
                FirstName = firstName,
                LastName = lastName,
                ExtraData = new byte[extraDataSize],
            };

            return WriteStateAsync();
        }
    }
}
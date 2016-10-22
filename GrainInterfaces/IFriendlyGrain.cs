namespace GrainInterfaces
{
    using System;
    using System.Threading.Tasks;
    using Orleans;

    public interface IFriendlyGrain : IGrainWithGuidKey
    {
        Task Initialize(Guid bestFriend, string firstName, string lastName, int extraDataSize);
    }
}

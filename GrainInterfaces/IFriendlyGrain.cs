namespace GrainInterfaces
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Orleans;
    using Orleans.Concurrency;

    public interface IFriendlyGrain : IGrainWithGuidKey
    {
        Task Initialize(IFriendlyGrain bestFriend, string firstName, string lastName, IList<IPetGrain> petNames, Immutable<byte[]> extraData);

        Task<string> GetFullName(string separator);
        Task<IEnumerable<string>> GetPetNames();
        Task<string> GetFriendNames(string separator, int count);

        Task<string> GetLastName();
        Task UpdateLastName(string newName);
    }
}

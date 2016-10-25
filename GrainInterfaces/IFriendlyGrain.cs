namespace GrainInterfaces
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Orleans;

    public interface IFriendlyGrain : IGrainWithGuidKey
    {
        Task Initialize(IFriendlyGrain bestFriend, string firstName, string lastName, IList<IPetGrain> petNames, int extraDataSize);

        Task<string> GetFullName(string separator);
        Task<IEnumerable<string>> GetPetNames();
        Task<string> GetFriendNames(string separator, int count);

        Task<string> GetLastName();
        Task UpdateLastName(string newName);
    }
}

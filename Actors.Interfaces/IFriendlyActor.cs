namespace Actors.Interfaces
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.ServiceFabric.Actors;

    public interface IFriendlyActor : IActor
    {
        Task Initialize(IFriendlyActor bestFriend, string firstName, string lastName, IList<IPetActor> pets, int extraDataSize);

        Task<string> GetFullName(string separator);
        Task<IEnumerable<string>> GetPetNames();
        Task<string> GetFriendNames(string separator, int count);
    }
}

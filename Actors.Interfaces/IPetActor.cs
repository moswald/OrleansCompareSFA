namespace Actors.Interfaces
{
    using System.Threading.Tasks;
    using Microsoft.ServiceFabric.Actors;

    public interface IPetActor : IActor
    {
        Task Initialize(IFriendlyActor owner, string name);
    }
}

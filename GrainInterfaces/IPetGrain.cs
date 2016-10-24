namespace GrainInterfaces
{
    using System.Threading.Tasks;
    using Orleans;

    public interface IPetGrain : IGrainWithGuidKey
    {
        Task Initialize(IFriendlyGrain owner, string name);

        Task<string> GetName();
    }
}

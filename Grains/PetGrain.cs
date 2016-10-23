namespace Grains
{
    using System.Threading.Tasks;
    using GrainInterfaces;
    using Orleans;

    class PetGrainState
    {
        public IFriendlyGrain Owner { get; set; }
        public string Name { get; set; }
    }

    class PetGrain : Grain<PetGrainState>, IPetGrain
    {
        public Task Initialize(IFriendlyGrain owner, string name)
        {
            State = new PetGrainState
            {
                Owner = owner,
                Name = name
            };

            return WriteStateAsync();
        }
    }
}

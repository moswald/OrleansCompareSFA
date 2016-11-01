namespace Grains
{
    using System.Threading.Tasks;
    using GrainInterfaces;
    using Orleans;
    using Orleans.Runtime;
    using Orleans.Storage;

    class PetGrainState
    {
        public IFriendlyGrain Owner { get; set; }
        public string Name { get; set; }
    }

    class PetGrain : Grain<PetGrainState>, IPetGrain
    {
        public async Task Initialize(IFriendlyGrain owner, string name)
        {
            var attempts = 0;
            do
        {
            State = new PetGrainState
            {
                Owner = owner,
                Name = name
            };

                try
                {
                    await WriteStateAsync();
                    break;
                }
                catch (OrleansException)
                {
                    await ReadStateAsync();

                    if (State.Name == name)
                    {
                        break;
                    }
                }
            } while (++attempts < 3);

            if (attempts == 3)
            {
                throw new InconsistentStateException($"After 3 attempts, could not write state for {this.GetPrimaryKey()}");
            } 
        }

        public Task<string> GetName() => Task.FromResult(State.Name);
    }
}

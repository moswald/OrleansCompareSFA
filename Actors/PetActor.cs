namespace Actors
{
    using System.Threading.Tasks;
    using Interfaces;
    using Microsoft.ServiceFabric.Actors;
    using Microsoft.ServiceFabric.Actors.Runtime;

    public class PetActor : Actor, IPetActor
    {
        const string OwnerState = nameof(OwnerState);
        const string NameState = nameof(NameState);

        public PetActor(ActorService actorService, ActorId actorId)
            : base(actorService, actorId)
        {
        }

        public Task Initialize(IFriendlyActor owner, string name)
        {
            StateManager.AddStateAsync(OwnerState, owner);
            StateManager.AddStateAsync(NameState, name);

            return Task.CompletedTask;
        }

        public Task<string> GetName() => StateManager.GetStateAsync<string>(NameState);
    }
}

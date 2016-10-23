namespace Actors
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Interfaces;
    using Microsoft.ServiceFabric.Actors;
    using Microsoft.ServiceFabric.Actors.Runtime;

    [StatePersistence(StatePersistence.Persisted)]
    class FriendlyActor : Actor, IFriendlyActor
    {
        const string BestFriendState = nameof(BestFriendState);
        const string FirstNameState = nameof(FirstNameState);
        const string LastNameState = nameof(LastNameState);
        const string PetsState = nameof(PetsState);
        const string ExtraDataState = nameof(ExtraDataState);

        public FriendlyActor(ActorService actorService, ActorId actorId)
            : base(actorService, actorId)
        {
        }

        public Task Initialize(ActorId bestFriend, string firstName, string lastName, IEnumerable<IPetActor> pets, int extraDataSize)
        {
            StateManager.AddStateAsync(BestFriendState, bestFriend);
            StateManager.AddStateAsync(FirstNameState, firstName);
            StateManager.AddStateAsync(LastNameState, lastName);
            StateManager.AddStateAsync(PetsState, pets);
            StateManager.AddStateAsync(ExtraDataState, new byte[extraDataSize]);

            return Task.CompletedTask;
        }
    }
}

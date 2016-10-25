namespace Actors
{
    using System.Collections.Generic;
    using System.Linq;
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

        Task IFriendlyActor.Initialize(IFriendlyActor bestFriend, string firstName, string lastName, IList<IPetActor> pets, int extraDataSize) =>
            Task.WhenAll(
                StateManager.AddStateAsync(BestFriendState, bestFriend),
                StateManager.AddStateAsync(FirstNameState, firstName),
                StateManager.AddStateAsync(LastNameState, lastName),
                StateManager.AddStateAsync(PetsState, pets),
                StateManager.AddStateAsync(ExtraDataState, new byte[extraDataSize]));

        async Task<string> IFriendlyActor.GetFullName(string separator)
        {
            var firstName = await StateManager.GetStateAsync<string>(FirstNameState);
            var lastName = await StateManager.GetStateAsync<string>(LastNameState);

            return firstName + separator + lastName;
        }

        async Task<IEnumerable<string>> IFriendlyActor.GetPetNames()
        {
            var pets = await StateManager.GetStateAsync<IEnumerable<IPetActor>>(PetsState);

            var names = pets.Select(pet => pet.GetName());
            return await Task.WhenAll(names);
        }

        async Task<string> IFriendlyActor.GetFriendNames(string separator, int count)
        {
            if (count == 0)
            {
                return await StateManager.GetStateAsync<string>(FirstNameState);
            }

            var bestFriend = await StateManager.GetStateAsync<IFriendlyActor>(BestFriendState);
            return await StateManager.GetStateAsync<string>(FirstNameState) + separator + await bestFriend.GetFriendNames(separator, --count);
        }
    }
}

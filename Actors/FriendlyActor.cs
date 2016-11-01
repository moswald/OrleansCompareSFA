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

        Task IFriendlyActor.Initialize(IFriendlyActor bestFriend, string firstName, string lastName, IList<IPetActor> pets, byte[] extraData) =>
            Task.WhenAll(
                StateManager.SetStateAsync(BestFriendState, bestFriend),
                StateManager.SetStateAsync(FirstNameState, firstName),
                StateManager.SetStateAsync(LastNameState, lastName),
                StateManager.SetStateAsync(PetsState, pets),
                StateManager.SetStateAsync(ExtraDataState, extraData));

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

        Task<string> IFriendlyActor.GetLastName() => StateManager.GetStateAsync<string>(LastNameState);
        Task IFriendlyActor.UpdateLastName(string newName) => StateManager.SetStateAsync(LastNameState, newName);
    }
}

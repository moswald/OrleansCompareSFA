﻿namespace Actors.Interfaces
{
    using System.Threading.Tasks;
    using Microsoft.ServiceFabric.Actors;

    public interface IFriendlyActor : IActor
    {
        Task Initialize(ActorId bestFriend, string firstName, string lastName, int extraDataSize);
    }
}
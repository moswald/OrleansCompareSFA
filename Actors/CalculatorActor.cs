namespace Actors
{
    using System.Threading.Tasks;
    using Interfaces;
    using Microsoft.ServiceFabric.Actors;
    using Microsoft.ServiceFabric.Actors.Runtime;

    [StatePersistence(StatePersistence.None)]
    class CalculatorActor : Actor, ICalculatorActor
    {
        public CalculatorActor(ActorService actorService, ActorId actorId)
            : base(actorService, actorId)
        {
        }

        Task<double> ICalculatorActor.Add(double a, double b) => Task.FromResult(a + b);
    }
}

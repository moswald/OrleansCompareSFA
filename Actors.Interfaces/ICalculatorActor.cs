namespace Actors.Interfaces
{
    using System.Threading.Tasks;
    using Microsoft.ServiceFabric.Actors;

    public interface ICalculatorActor : IActor
    {
        Task<double> Add(double a, double b);
    }
}

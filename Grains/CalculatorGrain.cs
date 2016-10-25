namespace Grains
{
    using System.Threading.Tasks;
    using GrainInterfaces;
    using Orleans;
    using Orleans.Concurrency;

    [StatelessWorker]
    class CalculatorGrain : Grain, ICalculatorGrain
    {
        Task<double> ICalculatorGrain.Add(double a, double b) => Task.FromResult(a + b);
    }
}

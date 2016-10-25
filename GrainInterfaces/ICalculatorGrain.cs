namespace GrainInterfaces
{
    using System.Threading.Tasks;
    using Orleans;

    public interface ICalculatorGrain : IGrainWithIntegerKey
    {
        Task<double> Add(double a, double b);
    }
}

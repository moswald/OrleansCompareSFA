namespace TestRunnerApi
{
    using System.Collections.Generic;
    using System.Fabric;
    using Microsoft.ServiceFabric.Services.Communication.Runtime;
    using Microsoft.ServiceFabric.Services.Runtime;

    sealed class TestRunnerApi : StatelessService
    {
        public TestRunnerApi(StatelessServiceContext context)
            : base(context)
        { }

        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            return new ServiceInstanceListener[]
            {
                new ServiceInstanceListener(serviceContext => new OwinCommunicationListener(Startup.ConfigureApp, serviceContext, ServiceEventSource.Current, "ServiceEndpoint"))
            };
        }
    }
}

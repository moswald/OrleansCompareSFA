using System.Web.Http;
using Owin;

namespace TestRunnerApi
{
    using System;
    using System.Fabric;
    using System.Text.RegularExpressions;
    using Orleans;
    using Orleans.Runtime.Configuration;

    public static class Startup
    {
        // This code configures Web API. The Startup class is specified as a type
        // parameter in the WebApp.Start method.
        public static void ConfigureApp(IAppBuilder appBuilder)
        {
            ConfigureOrleansClient();

            // Configure Web API for self-host. 
            var config = new HttpConfiguration { IncludeErrorDetailPolicy = IncludeErrorDetailPolicy.Always };

            config.MapHttpAttributeRoutes();

            appBuilder.UseWebApi(config);
        }

        static void ConfigureOrleansClient()
        {
            var appConfig = FabricRuntime.GetActivationContext().GetConfigurationPackageObject("Config");
            var siloTable = appConfig.Settings.Sections["Storage"].Parameters["SiloStorageConnectionString"].Value;
            var serviceName = new Uri("fabric:/OrleansCompareSFA/OrleansSiloHost");

            var config = new ClientConfiguration
            {
                DataConnectionString = siloTable,
                DeploymentId = Regex.Replace(serviceName.PathAndQuery.Trim('/'), "[^a-zA-Z0-9_]", "_"),
                GatewayProvider = ClientConfiguration.GatewayProviderType.AzureTable,
                ResponseTimeout = TimeSpan.FromMinutes(5)
            };

            GrainClient.Initialize(config);
        }
    }
}

namespace OrleansSiloHost
{
    using System;
    using System.Collections.Generic;
    using System.Fabric;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Orleans.ServiceFabric;
    using Microsoft.ServiceFabric.Services.Communication.Runtime;
    using Microsoft.ServiceFabric.Services.Runtime;
    using Nito.AsyncEx;
    using Orleans.Runtime;
    using Orleans.Runtime.Configuration;
    using OrleansDashboard;

    sealed class OrleansSiloHost : StatelessService
    {
        public OrleansSiloHost(StatelessServiceContext context)
            : base(context)
        { }

        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            return new[] { OrleansServiceListener.CreateStateless(GetClusterConfiguration()) };
        }

        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            // not going to actually do anything here - just exit when cancellationToken is signaled
            await cancellationToken.AsTask();
        }

        ClusterConfiguration GetClusterConfiguration()
        {
            var appConfig = Context.CodePackageActivationContext.GetConfigurationPackageObject("Config");

            var clusterConfig = new ClusterConfiguration();
            clusterConfig.Defaults.StatisticsCollectionLevel = StatisticsLevel.Verbose;
            clusterConfig.Defaults.StatisticsLogWriteInterval = TimeSpan.FromDays(7);
            clusterConfig.Defaults.TurnWarningLengthThreshold = TimeSpan.FromSeconds(5);
            clusterConfig.Defaults.TraceToConsole = false;
            clusterConfig.Defaults.DefaultTraceLevel = Severity.Info;

            clusterConfig.Globals.DeploymentId = Regex.Replace(Context.ServiceName.PathAndQuery.Trim('/'), "[^a-zA-Z0-9_]", "_");
            clusterConfig.Globals.LivenessType = GlobalConfiguration.LivenessProviderType.AzureTable;

            AddStorageProviders(clusterConfig, appConfig);

            ConfigureDashboard(clusterConfig.Globals);

            return clusterConfig;
        }

        static void AddStorageProviders(ClusterConfiguration clusterConfig, ConfigurationPackage appConfig)
        {
            var storageSettings = appConfig.Settings.Sections["Storage"].Parameters;

            clusterConfig.Globals.DataConnectionString = storageSettings["SiloStorageConnectionString"].Value;

            clusterConfig.Globals.ReminderServiceType = GlobalConfiguration.ReminderServiceProviderType.AzureTable;
            clusterConfig.Globals.DataConnectionStringForReminders = storageSettings["RemindersStorageConnectionString"].Value;

            var useJson = bool.Parse(storageSettings["GrainsUseJsonFormat"].Value);
            var indentJson = useJson && bool.Parse(storageSettings["GrainsUseIndentedJsonFormat"].Value);

            clusterConfig.AddAzureTableStorageProvider(
                providerName: "Default",
                connectionString: storageSettings["GrainStorageConnectionString"].Value,
                useJsonFormat: useJson,
                indentJson: indentJson);
        }

        void ConfigureDashboard(GlobalConfiguration config)
        {
            var port = Context.CodePackageActivationContext.GetEndpoint("Dashboard").Port;
            config.RegisterDashboard(port);
        }
    }
}

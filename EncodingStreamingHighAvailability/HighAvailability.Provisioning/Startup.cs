using Microsoft.Azure.Functions.Extensions.DependencyInjection;

[assembly: FunctionsStartup(typeof(HighAvailability.Provisioner.Startup))]

namespace HighAvailability.Provisioner
{
    using Azure.Storage.Queues;
    using HighAvailability.Services;
    using Microsoft.Azure.Functions.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection;
    using System;
    using System.Collections.Generic;

    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            var keyVaultName = Environment.GetEnvironmentVariable("KeyVaultName");
            var configService = new ConfigService(keyVaultName);
            configService.LoadConfigurationAsync().Wait();

            var provisioningCompletedEventQueue = new QueueClient(configService.StorageAccountConnectionString, configService.ProvisioningCompletedEventQueueName);
            provisioningCompletedEventQueue.CreateIfNotExists();

            var provisioningEventStorageService = new ProvisioningCompletedEventStorageService(provisioningCompletedEventQueue);

            var assetDataProvisioningService = new AssetDataProvisioningService(configService);
            var clearStreamingProvisioningService = new ClearStreamingProvisioningService(configService);
            var clearKeyStreamingProvisioningService = new ClearKeyStreamingProvisioningService(configService);

            var provisioningOrchestrator = new ProvisioningOrchestrator(new List<IProvisioningService> { assetDataProvisioningService, clearStreamingProvisioningService, clearKeyStreamingProvisioningService });

            builder.Services.AddSingleton<IProvisioningOrchestrator>(provisioningOrchestrator);
        }
    }
}

using Microsoft.Azure.Functions.Extensions.DependencyInjection;

[assembly: FunctionsStartup(typeof(HighAvailability.Provisioner.Startup))]

namespace HighAvailability.Provisioner
{
    using Azure.Storage.Queues;
    using HighAvailability.AzureStorage.Services;
    using HighAvailability.Factories;
    using HighAvailability.Interfaces;
    using HighAvailability.Services;
    using Microsoft.Azure.Functions.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Implements startup logic for provisioning Azure function.
    /// See for more details about dependency injection for Azure Functions
    /// https://docs.microsoft.com/en-us/azure/azure-functions/functions-dotnet-dependency-injection
    /// </summary>
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            var keyVaultName = Environment.GetEnvironmentVariable("KeyVaultName");
            var configService = new ConfigService(keyVaultName);
            configService.LoadConfigurationAsync().Wait();

            var provisioningCompletedEventQueue = new QueueClient(configService.StorageAccountConnectionString, configService.ProvisioningCompletedEventQueueName);
            provisioningCompletedEventQueue.CreateIfNotExists();

            var provisioningCompletedEventStorageService = new ProvisioningCompletedEventStorageService(provisioningCompletedEventQueue);

            var assetDataProvisioningService = new AssetDataProvisioningService(new MediaServiceInstanceFactory(configService), configService);
            var clearStreamingProvisioningService = new ClearStreamingProvisioningService(new MediaServiceInstanceFactory(configService), configService);
            var clearKeyStreamingProvisioningService = new ClearKeyStreamingProvisioningService(new MediaServiceInstanceFactory(configService), configService);

            // Instantiate the list of provisioning services to run for each provisioning request
            // These services run in the same order as in this list
            var provisioningOrchestrator = new ProvisioningOrchestrator(
                new List<IProvisioningService> 
                { 
                    assetDataProvisioningService, 
                    clearStreamingProvisioningService, 
                    clearKeyStreamingProvisioningService 
                }, 
                provisioningCompletedEventStorageService);

            builder.Services.AddSingleton<IProvisioningOrchestrator>(provisioningOrchestrator);
        }
    }
}

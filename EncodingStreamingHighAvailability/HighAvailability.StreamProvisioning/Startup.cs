using Microsoft.Azure.Functions.Extensions.DependencyInjection;

[assembly: FunctionsStartup(typeof(HighAvailability.StreamProvisioning.Startup))]

namespace HighAvailability.StreamProvisioning
{
    using Azure.Storage.Queues;
    using HighAvailability.Services;
    using Microsoft.Azure.Functions.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection;
    using System;

    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            var keyVaultName = Environment.GetEnvironmentVariable("KeyVaultName");
            var configService = new ConfigService(keyVaultName);
            configService.LoadConfigurationAsync().Wait();

            var streamProvisioningEventQueue = new QueueClient(configService.StorageAccountConnectionString, configService.StreamProvisioningEventQueueName);
            streamProvisioningEventQueue.CreateIfNotExists();

            var streamProvisioningEventStorageService = new StreamProvisioningEventStorageService(streamProvisioningEventQueue);
            var streamProvisioningService = new StreamProvisioningService(streamProvisioningEventStorageService, configService);

            builder.Services.AddSingleton<IStreamProvisioningService>(streamProvisioningService);
        }
    }
}

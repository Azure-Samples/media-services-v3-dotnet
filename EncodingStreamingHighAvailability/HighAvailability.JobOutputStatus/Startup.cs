using Microsoft.Azure.Functions.Extensions.DependencyInjection;

[assembly: FunctionsStartup(typeof(HighAvailability.JobOutputStatus.Startup))]

namespace HighAvailability.JobOutputStatus
{
    using Azure.Storage.Queues;
    using HighAvailability.Services;
    using Microsoft.Azure.Cosmos.Table;
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

            var tableStorageAccount = CloudStorageAccount.Parse(configService.TableStorageAccountConnectionString);
            var tableClient = tableStorageAccount.CreateCloudTableClient();

            var jobOutputStatusTable = tableClient.GetTableReference(configService.JobOutputStatusTableName);
            jobOutputStatusTable.CreateIfNotExists();
            var jobOutputStatusTableStorageService = new TableStorageService(jobOutputStatusTable);

            var streamProvisioningRequestQueue = new QueueClient(configService.StorageAccountConnectionString, configService.StreamProvisioningRequestQueueName);
            streamProvisioningRequestQueue.CreateIfNotExists();

            var jobOutputStatusStorageService = new JobOutputStatusStorageService(jobOutputStatusTableStorageService);
            var streamProvisioningRequestStorageService = new StreamProvisioningRequestStorageService(streamProvisioningRequestQueue);
            var jobOutputStatusService = new JobOutputStatusService(jobOutputStatusStorageService, streamProvisioningRequestStorageService);
            var eventGridService = new EventGridService();

            builder.Services.AddSingleton<IJobOutputStatusService>(jobOutputStatusService);
            builder.Services.AddSingleton<IEventGridService>(eventGridService);
        }
    }
}

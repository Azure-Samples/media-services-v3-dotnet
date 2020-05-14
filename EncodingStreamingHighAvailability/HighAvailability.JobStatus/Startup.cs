using Microsoft.Azure.Functions.Extensions.DependencyInjection;

[assembly: FunctionsStartup(typeof(HighAvailability.JobStatus.Startup))]

namespace HighAvailability.JobStatus
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

            var jobStatusTable = tableClient.GetTableReference(configService.JobStatusTableName);
            jobStatusTable.CreateIfNotExists();
            var jobStatusTableStorageService = new TableStorageService(jobStatusTable);

            var streamProvisioningRequestQueue = new QueueClient(configService.StorageAccountConnectionString, configService.StreamProvisioningRequestQueueName);
            streamProvisioningRequestQueue.CreateIfNotExists();

            var jobStatusStorageService = new JobStatusStorageService(jobStatusTableStorageService);
            var streamProvisioningRequestStorageService = new StreamProvisioningRequestStorageService(streamProvisioningRequestQueue);
            var jobStatusService = new JobStatusService(jobStatusStorageService, streamProvisioningRequestStorageService);
            var eventGridService = new EventGridService();

            builder.Services.AddSingleton<IJobStatusService>(jobStatusService);
            builder.Services.AddSingleton<IEventGridService>(eventGridService);
        }
    }
}

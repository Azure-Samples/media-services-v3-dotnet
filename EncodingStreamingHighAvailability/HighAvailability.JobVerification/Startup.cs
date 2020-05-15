using Microsoft.Azure.Functions.Extensions.DependencyInjection;

[assembly: FunctionsStartup(typeof(HighAvailability.JobVerification.Startup))]

namespace HighAvailability.JobVerification
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

            var mediaServiceInstanceHealthTable = tableClient.GetTableReference(configService.MediaServiceInstanceHealthTableName);
            mediaServiceInstanceHealthTable.CreateIfNotExists();
            var mediaServiceInstanceHealthTableStorageService = new TableStorageService(mediaServiceInstanceHealthTable);

            var jobStatusTable = tableClient.GetTableReference(configService.JobStatusTableName);
            jobStatusTable.CreateIfNotExists();
            var jobStatusTableStorageService = new TableStorageService(jobStatusTable);

            var streamProvisioningRequestQueue = new QueueClient(configService.StorageAccountConnectionString, configService.StreamProvisioningRequestQueueName);
            streamProvisioningRequestQueue.CreateIfNotExists();

            var jobStatusStorageService = new JobStatusStorageService(jobStatusTableStorageService);
            var mediaServiceInstanceHealthStorageService = new MediaServiceInstanceHealthStorageService(mediaServiceInstanceHealthTableStorageService);
            var mediaServiceInstanceHealthService = new MediaServiceInstanceHealthService(mediaServiceInstanceHealthStorageService, jobStatusStorageService, configService);
            var streamProvisioningRequestStorageService = new StreamProvisioningRequestStorageService(streamProvisioningRequestQueue);
            var jobVerificationService = new JobVerificationService(mediaServiceInstanceHealthService, jobStatusStorageService, streamProvisioningRequestStorageService, configService);

            builder.Services.AddSingleton<IJobVerificationService>(jobVerificationService);
        }
    }
}

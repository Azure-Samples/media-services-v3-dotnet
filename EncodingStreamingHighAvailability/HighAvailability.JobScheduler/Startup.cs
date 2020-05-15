using Microsoft.Azure.Functions.Extensions.DependencyInjection;

[assembly: FunctionsStartup(typeof(HighAvailability.JobScheduler.Startup))]

namespace HighAvailability.JobScheduler
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

            var jobVerificationRequestQueue = new QueueClient(configService.StorageAccountConnectionString, configService.JobVerificationRequestQueueName);
            jobVerificationRequestQueue.CreateIfNotExists();

            var jobStatusStorageService = new JobStatusStorageService(jobStatusTableStorageService);
            var mediaServiceInstanceHealthStorageService = new MediaServiceInstanceHealthStorageService(mediaServiceInstanceHealthTableStorageService);
            var mediaServiceInstanceHealthService = new MediaServiceInstanceHealthService(mediaServiceInstanceHealthStorageService, jobStatusStorageService, configService);
            var jobVerificationRequestStorageService = new JobVerificationRequestStorageService(jobVerificationRequestQueue);
            var jobSchedulerService = new JobSchedulerService(mediaServiceInstanceHealthService, jobVerificationRequestStorageService, jobStatusStorageService, configService);

            builder.Services.AddSingleton<IJobSchedulerService>(jobSchedulerService);
        }
    }
}

using Microsoft.Azure.Functions.Extensions.DependencyInjection;

[assembly: FunctionsStartup(typeof(HighAvailability.JobScheduling.Startup))]

namespace HighAvailability.JobScheduling
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

            var jobOutputStatusTable = tableClient.GetTableReference(configService.JobOutputStatusTableName);
            jobOutputStatusTable.CreateIfNotExists();
            var jobOutputStatusTableStorageService = new TableStorageService(jobOutputStatusTable);

            var jobVerificationRequestQueue = new QueueClient(configService.StorageAccountConnectionString, configService.JobVerificationRequestQueueName);
            jobVerificationRequestQueue.CreateIfNotExists();

            var jobOutputStatusStorageService = new JobOutputStatusStorageService(jobOutputStatusTableStorageService);
            var mediaServiceInstanceHealthStorageService = new MediaServiceInstanceHealthStorageService(mediaServiceInstanceHealthTableStorageService);
            var mediaServiceInstanceHealthService = new MediaServiceInstanceHealthService(mediaServiceInstanceHealthStorageService, jobOutputStatusStorageService, configService);
            var jobVerificationRequestStorageService = new JobVerificationRequestStorageService(jobVerificationRequestQueue);
            var jobSchedulerService = new JobSchedulingService(mediaServiceInstanceHealthService, jobVerificationRequestStorageService, jobOutputStatusStorageService, configService);

            builder.Services.AddSingleton<IJobSchedulingService>(jobSchedulerService);
        }
    }
}

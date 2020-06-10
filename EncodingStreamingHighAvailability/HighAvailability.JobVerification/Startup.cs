using Microsoft.Azure.Functions.Extensions.DependencyInjection;

[assembly: FunctionsStartup(typeof(HighAvailability.JobVerification.Startup))]

namespace HighAvailability.JobVerification
{
    using Azure.Storage.Queues;
    using HighAvailability.AzureStorage.Services;
    using HighAvailability.Factories;
    using HighAvailability.Interfaces;
    using HighAvailability.Services;
    using Microsoft.Azure.Cosmos.Table;
    using Microsoft.Azure.Functions.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection;
    using System;

    /// <summary>
    /// Implements startup logic for job verification Azure function.
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

            var tableStorageAccount = CloudStorageAccount.Parse(configService.TableStorageAccountConnectionString);
            var tableClient = tableStorageAccount.CreateCloudTableClient();

            var mediaServiceInstanceHealthTable = tableClient.GetTableReference(configService.MediaServiceInstanceHealthTableName);
            mediaServiceInstanceHealthTable.CreateIfNotExists();
            var mediaServiceInstanceHealthTableStorageService = new TableStorageService(mediaServiceInstanceHealthTable);

            var jobOutputStatusTable = tableClient.GetTableReference(configService.JobOutputStatusTableName);
            jobOutputStatusTable.CreateIfNotExists();
            var jobOutputStatusTableStorageService = new TableStorageService(jobOutputStatusTable);

            var mediaServiceCallHistoryTable = tableClient.GetTableReference(configService.MediaServiceCallHistoryTableName);
            mediaServiceCallHistoryTable.CreateIfNotExists();
            var mediaServiceCallHistoryTableStorageService = new TableStorageService(mediaServiceCallHistoryTable);

            var provisioningRequestQueue = new QueueClient(configService.StorageAccountConnectionString, configService.ProvisioningRequestQueueName);
            provisioningRequestQueue.CreateIfNotExists();

            var jobVerificationRequestQueue = new QueueClient(configService.StorageAccountConnectionString, configService.JobVerificationRequestQueueName);
            jobVerificationRequestQueue.CreateIfNotExists();

            var jobOutputStatusStorageService = new JobOutputStatusStorageService(jobOutputStatusTableStorageService);
            var mediaServiceCallHistoryStorageService = new MediaServiceCallHistoryStorageService(mediaServiceCallHistoryTableStorageService);
            var mediaServiceInstanceHealthStorageService = new MediaServiceInstanceHealthStorageService(mediaServiceInstanceHealthTableStorageService);
            var mediaServiceInstanceHealthService = new MediaServiceInstanceHealthService(mediaServiceInstanceHealthStorageService, jobOutputStatusStorageService, configService);
            var provisioningRequestStorageService = new ProvisioningRequestStorageService(provisioningRequestQueue);
            var jobVerificationRequestStorageService = new JobVerificationRequestStorageService(jobVerificationRequestQueue);

            var jobVerificationService = new JobVerificationService(
                mediaServiceInstanceHealthService, 
                jobOutputStatusStorageService, 
                provisioningRequestStorageService, 
                jobVerificationRequestStorageService, 
                new MediaServiceInstanceFactory(configService),
                mediaServiceCallHistoryStorageService,
                configService);

            builder.Services.AddSingleton<IJobVerificationService>(jobVerificationService);
        }
    }
}

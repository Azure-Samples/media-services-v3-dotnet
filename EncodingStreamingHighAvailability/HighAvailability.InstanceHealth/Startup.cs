using Microsoft.Azure.Functions.Extensions.DependencyInjection;

[assembly: FunctionsStartup(typeof(HighAvailability.InstanceHealth.Startup))]

namespace HighAvailability.InstanceHealth
{
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

            var jobStatusStorageService = new JobStatusStorageService(jobStatusTableStorageService);
            var mediaServiceInstanceHealthStorageService = new MediaServiceInstanceHealthStorageService(mediaServiceInstanceHealthTableStorageService);
            var mediaServiceInstanceHealthService = new MediaServiceInstanceHealthService(mediaServiceInstanceHealthStorageService, jobStatusStorageService, configService);
            var jobStatusSyncService = new JobStatusSyncService(mediaServiceInstanceHealthService, jobStatusStorageService, configService);

            builder.Services.AddSingleton<IMediaServiceInstanceHealthService>(mediaServiceInstanceHealthService);
            builder.Services.AddSingleton<IJobStatusSyncService>(jobStatusSyncService);
        }
    }
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Azure.Functions.Extensions.DependencyInjection;

[assembly: FunctionsStartup(typeof(HighAvailability.InstanceHealth.Startup))]

namespace HighAvailability.InstanceHealth
{
    using HighAvailability.AzureStorage.Services;
    using HighAvailability.Factories;
    using HighAvailability.Interfaces;
    using HighAvailability.Services;
    using Microsoft.Azure.Cosmos.Table;
    using Microsoft.Azure.Functions.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection;
    using System;

    /// <summary>
    /// Implements startup logic for instance health Azure function.
    /// See TBD link for more details.
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

            var jobOutputStatusStorageService = new JobOutputStatusStorageService(jobOutputStatusTableStorageService);
            var mediaServiceInstanceHealthStorageService = new MediaServiceInstanceHealthStorageService(mediaServiceInstanceHealthTableStorageService);
            var mediaServiceCallHistoryStorageService = new MediaServiceCallHistoryStorageService(mediaServiceCallHistoryTableStorageService);

            var mediaServiceInstanceHealthService = new MediaServiceInstanceHealthService(
                mediaServiceInstanceHealthStorageService,
                jobOutputStatusStorageService,
                mediaServiceCallHistoryStorageService,
                configService);

            var jobOutputStatusSyncService = new JobOutputStatusSyncService(
                mediaServiceInstanceHealthService,
                jobOutputStatusStorageService,
                new MediaServiceInstanceFactory(mediaServiceCallHistoryStorageService, configService),
                configService);

            builder.Services.AddSingleton<IMediaServiceInstanceHealthService>(mediaServiceInstanceHealthService);
            builder.Services.AddSingleton<IJobOutputStatusSyncService>(jobOutputStatusSyncService);
        }
    }
}

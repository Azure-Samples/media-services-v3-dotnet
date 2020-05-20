﻿namespace HighAvailability.Services
{
    using HighAvailability.Helpers;
    using HighAvailability.Models;
    using Microsoft.Azure.Cosmos.Table;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public class JobStatusStorageService : IJobStatusStorageService
    {
        private readonly ITableStorageService tableStorageService;

        public JobStatusStorageService(ITableStorageService tableStorageService)
        {
            this.tableStorageService = tableStorageService ?? throw new ArgumentNullException(nameof(tableStorageService));
        }

        public async Task<JobStatusModel> CreateOrUpdateAsync(JobStatusModel jobStatusModel, ILogger logger)
        {
            var jobStatusResult = await this.tableStorageService.CreateOrUpdateAsync(new JobStatusModelTableEntity(jobStatusModel)).ConfigureAwait(false);

            var jobStatusModelResult = jobStatusResult.GetJobStatusModel();
            logger.LogInformation($"JobStatusStorageService::CreateOrUpdateAsync completed: jobStatusModelResult={LogHelper.FormatObjectForLog(jobStatusModelResult)}");

            return jobStatusModelResult;
        }

        public async Task<JobStatusModel> GetLatestJobStatusAsync(string jobName, string jobOutputAssetName)
        {
            return (await this.ListAsync(jobName, jobOutputAssetName).ConfigureAwait(false)).OrderByDescending(i => i.EventTime).FirstOrDefault();
        }

        public async Task<IEnumerable<JobStatusModel>> ListAsync(string jobName, string jobOutputAssetName)
        {
            var rangeQuery =
                   new TableQuery<JobStatusModelTableEntity>().Where(
                       TableQuery.CombineFilters(
                           TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, jobName),
                           TableOperators.And,
                           TableQuery.GenerateFilterCondition(nameof(JobStatusModelTableEntity.JobOutputAssetName), QueryComparisons.Equal, jobOutputAssetName)
                           )
                       );

            return (await this.tableStorageService.QueryDataAsync(rangeQuery).ConfigureAwait(false)).Select(i => i.GetJobStatusModel());
        }

        public async Task<IEnumerable<JobStatusModel>> ListByMediaServiceAccountNameAsync(string mediaServiceAccountName, int timeWindowInMinutesToLoadJobs)
        {
            var rangeQuery =
                    new TableQuery<JobStatusModelTableEntity>().Where(
                        TableQuery.CombineFilters(
                            TableQuery.GenerateFilterCondition(nameof(JobStatusModelTableEntity.MediaServiceAccountName), QueryComparisons.Equal, mediaServiceAccountName),
                            TableOperators.And,
                            TableQuery.GenerateFilterConditionForDate(nameof(JobStatusModelTableEntity.EventTime), QueryComparisons.GreaterThanOrEqual, DateTime.UtcNow.AddMinutes(-timeWindowInMinutesToLoadJobs))
                            )
                        );

            return (await this.tableStorageService.QueryDataAsync(rangeQuery).ConfigureAwait(false)).Select(i => i.GetJobStatusModel());
        }

        public async Task<IEnumerable<JobStatusModel>> ListAsync()
        {
            return (await this.tableStorageService.ListAsync<JobStatusModelTableEntity>().ConfigureAwait(false)).Select(i => i.GetJobStatusModel());
        }
    }
}

namespace HighAvailability.Services
{
    using HighAvailability.Helpers;
    using HighAvailability.Models;
    using Microsoft.Azure.Cosmos.Table;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public class JobOutputStatusStorageService : IJobOutputStatusStorageService
    {
        private readonly ITableStorageService tableStorageService;

        public JobOutputStatusStorageService(ITableStorageService tableStorageService)
        {
            this.tableStorageService = tableStorageService ?? throw new ArgumentNullException(nameof(tableStorageService));
        }

        public async Task<JobOutputStatusModel> CreateOrUpdateAsync(JobOutputStatusModel jobOutputStatusModel, ILogger logger)
        {
            var jobOutputStatusResult = await this.tableStorageService.CreateOrUpdateAsync(new JobOutputStatusModelTableEntity(jobOutputStatusModel)).ConfigureAwait(false);

            var jobOutputStatusModelResult = jobOutputStatusResult.GetJobOutputStatusModel();
            logger.LogInformation($"JobOutputStatusStorageService::CreateOrUpdateAsync completed: jobOutputStatusModelResult={LogHelper.FormatObjectForLog(jobOutputStatusModelResult)}");

            return jobOutputStatusModelResult;
        }

        public async Task<JobOutputStatusModel> GetLatestJobOutputStatusAsync(string jobName, string jobOutputAssetName)
        {
            return (await this.ListAsync(jobName, jobOutputAssetName).ConfigureAwait(false)).OrderByDescending(i => i.EventTime).FirstOrDefault();
        }

        public async Task<IEnumerable<JobOutputStatusModel>> ListAsync(string jobName, string jobOutputAssetName)
        {
            var rangeQuery =
                   new TableQuery<JobOutputStatusModelTableEntity>().Where(
                       TableQuery.CombineFilters(
                           TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, jobName),
                           TableOperators.And,
                           TableQuery.GenerateFilterCondition(nameof(JobOutputStatusModelTableEntity.JobOutputAssetName), QueryComparisons.Equal, jobOutputAssetName)
                           )
                       );

            return (await this.tableStorageService.QueryDataAsync(rangeQuery).ConfigureAwait(false)).Select(i => i.GetJobOutputStatusModel());
        }

        public async Task<IEnumerable<JobOutputStatusModel>> ListByMediaServiceAccountNameAsync(string mediaServiceAccountName, int timeWindowInMinutesToLoadJobs)
        {
            var rangeQuery =
                    new TableQuery<JobOutputStatusModelTableEntity>().Where(
                        TableQuery.CombineFilters(
                            TableQuery.GenerateFilterCondition(nameof(JobOutputStatusModelTableEntity.MediaServiceAccountName), QueryComparisons.Equal, mediaServiceAccountName),
                            TableOperators.And,
                            TableQuery.GenerateFilterConditionForDate(nameof(JobOutputStatusModelTableEntity.EventTime), QueryComparisons.GreaterThanOrEqual, DateTime.UtcNow.AddMinutes(-timeWindowInMinutesToLoadJobs))
                            )
                        );

            return (await this.tableStorageService.QueryDataAsync(rangeQuery).ConfigureAwait(false)).Select(i => i.GetJobOutputStatusModel());
        }

        public async Task<IEnumerable<JobOutputStatusModel>> ListAsync()
        {
            return (await this.tableStorageService.ListAsync<JobOutputStatusModelTableEntity>().ConfigureAwait(false)).Select(i => i.GetJobOutputStatusModel());
        }
    }
}

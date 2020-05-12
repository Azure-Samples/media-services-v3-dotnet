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

    public class JobStatusStorageService : IJobStatusStorageService
    {
        private readonly TableStorageService tableStorageService;

        public JobStatusStorageService(TableStorageService tableStorageService)
        {
            this.tableStorageService = tableStorageService ?? throw new ArgumentNullException(nameof(tableStorageService));
        }

        public async Task<JobStatusModel> CreateOrUpdateAsync(JobStatusModel jobStatusModel, ILogger logger)
        {
            if (jobStatusModel == null)
            {
                throw new ArgumentNullException(nameof(jobStatusModel));
            }

            var jobStatusResult = await this.tableStorageService.CreateOrUpdateAsync(new JobStatusModelTableEntity(jobStatusModel)).ConfigureAwait(false);

            var jobStatusModelResult = jobStatusResult.GetJobStatusModel();
            logger.LogInformation($"JobStatusStorageService::CreateOrUpdateAsync completed: jobStatusModelResult={LogHelper.FormatObjectForLog(jobStatusModelResult)}");

            return jobStatusModelResult;
        }

        public Task<JobStatusModel> GetAsync(string id)
        {
            throw new NotImplementedException();
        }

        public async Task<JobStatusModel> GetLatestJobStatusAsync(string jobName)
        {
            return (await this.ListAsync(jobName).ConfigureAwait(false)).OrderByDescending(i => i.EventTime).FirstOrDefault();
        }

        public async Task<IEnumerable<JobStatusModel>> ListAsync(string jobName)
        {
            var rangeQuery = new TableQuery<JobStatusModelTableEntity>().Where(
                TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, jobName));

            return (await this.tableStorageService.QueryDataAsync<JobStatusModelTableEntity>(rangeQuery).ConfigureAwait(false)).Select(i => i.GetJobStatusModel());
        }

        public async Task<IEnumerable<JobStatusModel>> ListAsync()
        {
            return (await this.tableStorageService.ListAsync<JobStatusModelTableEntity>().ConfigureAwait(false)).Select(i => i.GetJobStatusModel());
        }
    }
}

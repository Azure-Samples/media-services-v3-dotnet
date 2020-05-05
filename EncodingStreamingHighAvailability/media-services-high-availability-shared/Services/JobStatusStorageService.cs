namespace media_services_high_availability_shared.Services
{
    using media_services_high_availability_shared.Helpers;
    using media_services_high_availability_shared.Models;
    using Microsoft.Azure.Cosmos.Table;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public class JobStatusStorageService : IJobStatusStorageService
    {
        private readonly CloudTable table;
        private readonly ILogger logger;
        private const int takeCount = 100;

        public JobStatusStorageService(CloudTable table, ILogger logger)
        {
            this.table = table ?? throw new ArgumentNullException(nameof(table));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<JobStatusModel> CreateOrUpdateAsync(JobStatusModel jobStatusModel)
        {
            if (jobStatusModel == null)
            {
                throw new ArgumentNullException(nameof(jobStatusModel));
            }

            var jobStatusModelTableEntity = new JobStatusModelTableEntity(jobStatusModel);
            var insertOrMergeOperation = TableOperation.InsertOrMerge(jobStatusModelTableEntity);

            var result = await this.table.ExecuteAsync(insertOrMergeOperation).ConfigureAwait(false);
            var jobStatusResult = result.Result as JobStatusModelTableEntity;

            if (jobStatusResult == null)
            {
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                throw new Exception("Got error callig Table API");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
            }

            var jobStatusModelResult = jobStatusResult.GetJobStatusModel();
            this.logger.LogInformation($"JobStatusStorageService::CreateOrUpdateAsync completed: jobStatusModelResult={LogHelper.FormatObjectForLog(jobStatusModelResult)}");

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
            rangeQuery.TakeCount = takeCount;

            return await this.QueryData(rangeQuery).ConfigureAwait(false);
        }

        public async Task<IEnumerable<JobStatusModel>> ListAsync()
        {
            var rangeQuery = new TableQuery<JobStatusModelTableEntity>
            {
                TakeCount = takeCount
            };
            return await this.QueryData(rangeQuery).ConfigureAwait(false);
        }

        private async Task<IEnumerable<JobStatusModel>> QueryData(TableQuery<JobStatusModelTableEntity> rangeQuery)
        {
            var results = new List<JobStatusModel>();
            TableContinuationToken? token = null;
            do
            {
                // Execute the query, passing in the continuation token.
                // The first time this method is called, the continuation token is null. If there are more results, the call
                // populates the continuation token for use in the next call.
                var segment = await this.table.ExecuteQuerySegmentedAsync(rangeQuery, token).ConfigureAwait(false);

                // Save the continuation token for the next call to ExecuteQuerySegmentedAsync
                token = segment.ContinuationToken;

                results.AddRange(segment.Results.Select(i => i.GetJobStatusModel()));
            }
            while (token != null);

            return results;
        }
    }
}

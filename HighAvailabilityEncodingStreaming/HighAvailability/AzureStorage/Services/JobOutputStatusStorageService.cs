// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace HighAvailability.AzureStorage.Services
{
    using HighAvailability.AzureStorage.Models;
    using HighAvailability.Helpers;
    using HighAvailability.Interfaces;
    using HighAvailability.Models;
    using Microsoft.Azure.Cosmos.Table;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// This class implements write and read job output status events using Azure Table Storage.
    /// </summary>
    public class JobOutputStatusStorageService : IJobOutputStatusStorageService
    {
        /// <summary>
        /// Table storage service
        /// </summary>
        private readonly ITableStorageService tableStorageService;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="tableStorageService">Table storage service</param>
        public JobOutputStatusStorageService(ITableStorageService tableStorageService)
        {
            this.tableStorageService = tableStorageService ?? throw new ArgumentNullException(nameof(tableStorageService));
        }

        /// <summary>
        /// Creates or updates job output status event record
        /// </summary>
        /// <param name="jobOutputStatusModel">Data to store</param>
        /// <param name="logger">Logger to log</param>
        /// <returns>Stored model</returns>
        public async Task<JobOutputStatusModel> CreateOrUpdateAsync(JobOutputStatusModel jobOutputStatusModel, ILogger logger)
        {
            var jobOutputStatusResult = await this.tableStorageService.CreateOrUpdateAsync(new JobOutputStatusModelTableEntity(jobOutputStatusModel)).ConfigureAwait(false);

            var jobOutputStatusModelResult = jobOutputStatusResult.GetJobOutputStatusModel();
            logger.LogInformation($"JobOutputStatusStorageService::CreateOrUpdateAsync completed: jobOutputStatusModelResult={LogHelper.FormatObjectForLog(jobOutputStatusModelResult)}");

            return jobOutputStatusModelResult;
        }

        /// <summary>
        /// Reads latest job output status record for a given job name and output asset name.
        /// </summary>
        /// <param name="jobName">Job name to load data for</param>
        /// <param name="jobOutputAssetName">Output asset name to load data for</param>
        /// <returns>Latest job output status record</returns>
        public async Task<JobOutputStatusModel> GetLatestJobOutputStatusAsync(string jobName, string jobOutputAssetName)
        {
            // List all records for a given condition and find latest one
            return (await this.ListAsync(jobName, jobOutputAssetName).ConfigureAwait(false)).OrderByDescending(i => i.EventTime).FirstOrDefault();
        }

        /// <summary>
        /// Reads all job output status records for a given job name and output asset name.
        /// </summary>
        /// <param name="jobName">Job name to load data for</param>
        /// <param name="jobOutputAssetName">Output asset name to load data for</param>
        /// <returns>List of job output status records</returns>
        public async Task<IEnumerable<JobOutputStatusModel>> ListAsync(string jobName, string jobOutputAssetName)
        {
            // PartitionKey contains job name
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

        /// <summary>
        /// Reads all records for a given account name and time duration condition
        /// </summary>
        /// <param name="mediaServiceAccountName">Account name to load data for</param>
        /// <param name="timeWindowInMinutesToLoadJobs">How far back to go to load data</param>
        /// <returns>List of job output status records</returns>
        public async Task<IEnumerable<JobOutputStatusModel>> ListByMediaServiceAccountNameAsync(string mediaServiceAccountName, int timeWindowInMinutesToLoadJobs)
        {
            // Table storage implementation for CosmosDb by default indexes all the fields, this query should be fast. 
            // If old table storage is used that is build on old Azure Table storage service, this query may result in full table scan and could be very expensive to run.
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
    }
}

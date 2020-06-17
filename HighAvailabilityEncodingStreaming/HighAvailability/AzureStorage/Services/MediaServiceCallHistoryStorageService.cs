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
    /// This class implements methods to write and read Media Service call history records using Azure Table Storage.
    /// </summary>
    public class MediaServiceCallHistoryStorageService : IMediaServiceCallHistoryStorageService
    {
        /// <summary>
        /// Table storage service
        /// </summary>
        private readonly ITableStorageService tableStorageService;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="tableStorageService">Table storage service</param>
        public MediaServiceCallHistoryStorageService(ITableStorageService tableStorageService)
        {
            this.tableStorageService = tableStorageService ?? throw new ArgumentNullException(nameof(tableStorageService));
        }

        /// <summary>
        /// Creates or updates Media Service call history record
        /// </summary>
        /// <param name="mediaServiceCallHistoryModel">Data to store</param>
        /// <param name="logger">Logger to log</param>
        /// <returns>Stored model</returns>
        public async Task<MediaServiceCallHistoryModel> CreateOrUpdateAsync(MediaServiceCallHistoryModel mediaServiceCallHistoryModel, ILogger logger)
        {
            var mediaServiceCallHistoryResult = await this.tableStorageService.CreateOrUpdateAsync(new MediaServiceCallHistoryModelTableEntity(mediaServiceCallHistoryModel)).ConfigureAwait(false);

            var mediaServiceCallHistoryModelResult = mediaServiceCallHistoryResult.GetMediaServiceCallHistoryModel();
            logger.LogInformation($"MediaServiceCallHistoryStorageService::CreateOrUpdateAsync completed: mediaServiceCallHistoryModel={LogHelper.FormatObjectForLog(mediaServiceCallHistoryModelResult)}");

            return mediaServiceCallHistoryModelResult;
        }

        /// <summary>
        /// Reads all records for a given account name and time duration condition
        /// </summary>
        /// <param name="mediaServiceAccountName">Account name to load data for</param>
        /// <param name="timeWindowInMinutesToLoadData">How far back to go to load data</param>
        /// <returns>List of Media Service call history records</returns>
        public async Task<IEnumerable<MediaServiceCallHistoryModel>> ListByMediaServiceAccountNameAsync(string mediaServiceAccountName, int timeWindowInMinutesToLoadData)
        {
            // Table storage implementation for CosmosDb by default indexes all the fields, this query should be fast. 
            // If old table storage is used that is build on old Azure Table storage service, this query may result in full table scan and could be very expensive to run.
            var rangeQuery =
                    new TableQuery<MediaServiceCallHistoryModelTableEntity>().Where(
                        TableQuery.CombineFilters(
                            TableQuery.GenerateFilterCondition(nameof(MediaServiceCallHistoryModel.MediaServiceAccountName), QueryComparisons.Equal, mediaServiceAccountName),
                            TableOperators.And,
                            TableQuery.GenerateFilterConditionForDate(nameof(MediaServiceCallHistoryModel.EventTime), QueryComparisons.GreaterThanOrEqual, DateTime.UtcNow.AddMinutes(-timeWindowInMinutesToLoadData))
                            )
                        );

            return (await this.tableStorageService.QueryDataAsync(rangeQuery).ConfigureAwait(false)).Select(i => i.GetMediaServiceCallHistoryModel());
        }
    }
}

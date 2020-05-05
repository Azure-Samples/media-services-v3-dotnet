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

    public class MediaServiceInstanceHealthStorageService : IMediaServiceInstanceHealthStorageService
    {
        private readonly CloudTable table;
        private readonly ILogger logger;
        private const int takeCount = 100;
        private static DateTime minDateTimeForTableStorage = new DateTime(1900, 1, 1);

        public MediaServiceInstanceHealthStorageService(CloudTable table, ILogger logger)
        {
            this.table = table ?? throw new ArgumentNullException(nameof(table));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<MediaServiceInstanceHealthModel> CreateOrUpdateAsync(MediaServiceInstanceHealthModel mediaServiceInstanceHealthModel)
        {
            if (mediaServiceInstanceHealthModel == null)
            {
                throw new ArgumentNullException(nameof(mediaServiceInstanceHealthModel));
            }

            var verifiedModel = new MediaServiceInstanceHealthModel
            {
                IsHealthy = mediaServiceInstanceHealthModel.IsHealthy,
                LastFailedJob = VerifyMinValue(mediaServiceInstanceHealthModel.LastFailedJob),
                LastSubmittedJob = VerifyMinValue(mediaServiceInstanceHealthModel.LastSubmittedJob),
                LastSuccessfulJob = VerifyMinValue(mediaServiceInstanceHealthModel.LastSuccessfulJob),
                LastUpdated = VerifyMinValue(mediaServiceInstanceHealthModel.LastUpdated),
                MediaServiceAccountName = mediaServiceInstanceHealthModel.MediaServiceAccountName
            };

            var mediaServiceInstanceHealthModelTableEntity = new MediaServiceInstanceHealthModelTableEntity(verifiedModel);
            var insertOrMergeOperation = TableOperation.InsertOrMerge(mediaServiceInstanceHealthModelTableEntity);

            var result = await this.table.ExecuteAsync(insertOrMergeOperation).ConfigureAwait(false);
            var mediaServiceInstanceHealthResult = result.Result as MediaServiceInstanceHealthModelTableEntity;

            if (mediaServiceInstanceHealthResult == null)
            {
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                throw new Exception("Got error callig Table API");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
            }

            var mediaServiceInstanceHealthModelResult = mediaServiceInstanceHealthResult.GetMediaServiceInstanceHealthModel();
            this.logger.LogInformation($"MediaServiceInstanceHealthStorageService::CreateOrUpdateAsync completed: mediaServiceInstanceHealthModelResult={LogHelper.FormatObjectForLog(mediaServiceInstanceHealthModelResult)}");

            return mediaServiceInstanceHealthModelResult;
        }

        public async Task<MediaServiceInstanceHealthModel> GetAsync(string mediaServiceName)
        {
            var retrieveOperation = TableOperation.Retrieve<MediaServiceInstanceHealthModelTableEntity>(mediaServiceName, MediaServiceInstanceHealthModelTableEntity.DefaultRowKeyValue);
            var result = await this.table.ExecuteAsync(retrieveOperation).ConfigureAwait(false);
            var model = result.Result as MediaServiceInstanceHealthModelTableEntity;

            if (model == null)
            {
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                throw new Exception("Got error callig Table API");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
            }

            return model.GetMediaServiceInstanceHealthModel();
        }

        public async Task<IEnumerable<MediaServiceInstanceHealthModel>> ListAsync()
        {
            var rangeQuery = new TableQuery<MediaServiceInstanceHealthModelTableEntity>
            {
                TakeCount = takeCount
            };
            return await this.QueryData(rangeQuery).ConfigureAwait(false);
        }

        public async Task<MediaServiceInstanceHealthModel> UpdateProcessedJobStateAsync(string mediaServiceName, bool isJobCompletedSuccessfully, DateTime eventDateTime)
        {
            var retrieveOperation = TableOperation.Retrieve<MediaServiceInstanceHealthModelTableEntity>(mediaServiceName, MediaServiceInstanceHealthModelTableEntity.DefaultRowKeyValue);
            var result = await this.table.ExecuteAsync(retrieveOperation).ConfigureAwait(false);
            var mediaServiceInstanceHealthResult = result.Result as MediaServiceInstanceHealthModelTableEntity;

            // TBD not sure if this is the best way to see if operation was successful
            if (mediaServiceInstanceHealthResult == null)
            {
                throw new Exception($"Media service instance is not registered: mediaServiceName={mediaServiceName}");
            }

            eventDateTime = VerifyMinValue(eventDateTime);

            mediaServiceInstanceHealthResult.LastUpdated = eventDateTime;
            if (isJobCompletedSuccessfully)
            {
                mediaServiceInstanceHealthResult.LastSuccessfulJob = eventDateTime;
            }
            else
            {
                mediaServiceInstanceHealthResult.LastFailedJob = eventDateTime;
            }

            var mergeOperation = TableOperation.Merge(mediaServiceInstanceHealthResult);
            result = await this.table.ExecuteAsync(mergeOperation).ConfigureAwait(false);
            mediaServiceInstanceHealthResult = result.Result as MediaServiceInstanceHealthModelTableEntity;

            if (mediaServiceInstanceHealthResult == null)
            {
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                throw new Exception("Got error callig Table API");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
            }

            return mediaServiceInstanceHealthResult.GetMediaServiceInstanceHealthModel();
        }

        public async Task<MediaServiceInstanceHealthModel> UpdateSubmittedJobStateAsync(string mediaServiceName, DateTime eventDateTime)
        {
            var retrieveOperation = TableOperation.Retrieve<MediaServiceInstanceHealthModelTableEntity>(mediaServiceName, MediaServiceInstanceHealthModelTableEntity.DefaultRowKeyValue);
            var result = await this.table.ExecuteAsync(retrieveOperation).ConfigureAwait(false);
            var mediaServiceInstanceHealthResult = result.Result as MediaServiceInstanceHealthModelTableEntity;

            // TBD not sure if this is the best way to see if operation was successful
            if (mediaServiceInstanceHealthResult == null)
            {
                throw new Exception($"Media service instance is not registered: mediaServiceName={mediaServiceName}");
            }

            eventDateTime = VerifyMinValue(eventDateTime);
            mediaServiceInstanceHealthResult.LastUpdated = eventDateTime;
            mediaServiceInstanceHealthResult.LastSubmittedJob = eventDateTime;

            var mergeOperation = TableOperation.Merge(mediaServiceInstanceHealthResult);
            result = await this.table.ExecuteAsync(mergeOperation).ConfigureAwait(false);
            mediaServiceInstanceHealthResult = result.Result as MediaServiceInstanceHealthModelTableEntity;

            if (mediaServiceInstanceHealthResult == null)
            {
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                throw new Exception("Got error callig Table API");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
            }

            return mediaServiceInstanceHealthResult.GetMediaServiceInstanceHealthModel();
        }

        public async Task<MediaServiceInstanceHealthModel> UpdateHealthStateAsync(string mediaServiceName, bool isHealthy, DateTime eventDateTime)
        {
            var retrieveOperation = TableOperation.Retrieve<MediaServiceInstanceHealthModelTableEntity>(mediaServiceName, MediaServiceInstanceHealthModelTableEntity.DefaultRowKeyValue);
            var result = await this.table.ExecuteAsync(retrieveOperation).ConfigureAwait(false);
            var mediaServiceInstanceHealthResult = result.Result as MediaServiceInstanceHealthModelTableEntity;

            // TBD not sure if this is the best way to see if operation was successful
            if (mediaServiceInstanceHealthResult == null)
            {
                throw new Exception($"Media service instance is not registered: mediaServiceName={mediaServiceName}");
            }

            eventDateTime = VerifyMinValue(eventDateTime);
            mediaServiceInstanceHealthResult.LastUpdated = eventDateTime;
            mediaServiceInstanceHealthResult.IsHealthy = isHealthy;

            var mergeOperation = TableOperation.Merge(mediaServiceInstanceHealthResult);
            result = await this.table.ExecuteAsync(mergeOperation).ConfigureAwait(false);
            mediaServiceInstanceHealthResult = result.Result as MediaServiceInstanceHealthModelTableEntity;

            if (mediaServiceInstanceHealthResult == null)
            {
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                throw new Exception("Got error callig Table API");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
            }

            return mediaServiceInstanceHealthResult.GetMediaServiceInstanceHealthModel();
        }

        private async Task<IEnumerable<MediaServiceInstanceHealthModel>> QueryData(TableQuery<MediaServiceInstanceHealthModelTableEntity> rangeQuery)
        {
            var results = new List<MediaServiceInstanceHealthModel>();
            TableContinuationToken? token = null;
            do
            {
                // Execute the query, passing in the continuation token.
                // The first time this method is called, the continuation token is null. If there are more results, the call
                // populates the continuation token for use in the next call.
                var segment = await this.table.ExecuteQuerySegmentedAsync(rangeQuery, token).ConfigureAwait(false);

                // Save the continuation token for the next call to ExecuteQuerySegmentedAsync
                token = segment.ContinuationToken;

                results.AddRange(segment.Results.Select(i => i.GetMediaServiceInstanceHealthModel()));
            }
            while (token != null);

            return results;
        }

        private static DateTime VerifyMinValue(DateTime dateTime)
        {
            return dateTime > minDateTimeForTableStorage ? dateTime : minDateTimeForTableStorage;
        }
    }
}

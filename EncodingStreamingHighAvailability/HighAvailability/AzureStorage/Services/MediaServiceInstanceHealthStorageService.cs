namespace HighAvailability.AzureStorage.Services
{
    using HighAvailability.AzureStorage.Models;
    using HighAvailability.Helpers;
    using HighAvailability.Interfaces;
    using HighAvailability.Models;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Implements class to write and read Azure Media Services instance health data using Azure Table Storage
    /// </summary>
    public class MediaServiceInstanceHealthStorageService : IMediaServiceInstanceHealthStorageService
    {
        /// <summary>
        /// Azure Table Storage does not support DateTime.MinValue, this date is used as min date for data stored
        /// </summary>
        private static DateTimeOffset minDateTimeForTableStorage = new DateTimeOffset(1900, 1, 1, 0, 0, 0, TimeSpan.FromSeconds(0));

        /// <summary>
        /// Table storage service
        /// </summary>
        private readonly ITableStorageService tableStorageService;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="tableStorageService">Table storage service</param>
        public MediaServiceInstanceHealthStorageService(ITableStorageService tableStorageService)
        {
            this.tableStorageService = tableStorageService ?? throw new ArgumentNullException(nameof(tableStorageService));
        }

        /// <summary>
        /// Stores Azure Media Services instance health data.
        /// </summary>
        /// <param name="mediaServiceInstanceHealthModel">Data to store</param>
        /// <param name="logger">Logger to log data</param>
        /// <returns>Created Azure Media Services instance health data record</returns>
        public async Task<MediaServiceInstanceHealthModel> CreateOrUpdateAsync(MediaServiceInstanceHealthModel mediaServiceInstanceHealthModel, ILogger logger)
        {
            // update all date fields to be at least minDateTimeForTableStorage
            var verifiedModel = new MediaServiceInstanceHealthModel
            {
                HealthState = mediaServiceInstanceHealthModel.HealthState,
                LastUpdated = VerifyMinValue(mediaServiceInstanceHealthModel.LastUpdated),
                MediaServiceAccountName = mediaServiceInstanceHealthModel.MediaServiceAccountName,
                IsEnabled = mediaServiceInstanceHealthModel.IsEnabled
            };

            var mediaServiceInstanceHealthResult = await this.tableStorageService.CreateOrUpdateAsync(new MediaServiceInstanceHealthModelTableEntity(verifiedModel)).ConfigureAwait(false);

            var mediaServiceInstanceHealthModelResult = mediaServiceInstanceHealthResult.GetMediaServiceInstanceHealthModel();
            logger.LogInformation($"MediaServiceInstanceHealthStorageService::CreateOrUpdateAsync completed: mediaServiceInstanceHealthModelResult={LogHelper.FormatObjectForLog(mediaServiceInstanceHealthModelResult)}");

            return mediaServiceInstanceHealthModelResult;
        }

        /// <summary>
        /// Gets specific Azure Media Services instance health data record.
        /// </summary>
        /// <param name="mediaServiceName">Azure Media Services instance account name</param>
        /// <returns>Azure Media Services instance health data record</returns>
        public async Task<MediaServiceInstanceHealthModel> GetAsync(string mediaServiceName)
        {
            var model = await this.tableStorageService.GetAsync<MediaServiceInstanceHealthModelTableEntity>(mediaServiceName, MediaServiceInstanceHealthModelTableEntity.DefaultRowKeyValue).ConfigureAwait(false);
            return model.GetMediaServiceInstanceHealthModel();
        }

        /// <summary>
        /// Lists all available Azure Media Services instance health data records
        /// </summary>
        /// <returns>List of Azure Media Services instance health data records</returns>
        public async Task<IEnumerable<MediaServiceInstanceHealthModel>> ListAsync()
        {
            return (await this.tableStorageService.ListAsync<MediaServiceInstanceHealthModelTableEntity>().ConfigureAwait(false)).Select(i => i.GetMediaServiceInstanceHealthModel());
        }

        /// <summary>
        /// Updates health state for the specific Azure Media Services instance health data record
        /// </summary>
        /// <param name="mediaServiceName">Azure Media Services instance account name</param>
        /// <param name="instanceHealthState">health state</param>
        /// <param name="eventDateTime">update record timestamp</param>
        /// <returns>Azure Media Services instance health data record</returns>
        public async Task<MediaServiceInstanceHealthModel> UpdateHealthStateAsync(string mediaServiceName, InstanceHealthState instanceHealthState, DateTimeOffset eventDateTime)
        {
            // Get the current record
            var getResult = await this.tableStorageService.GetAsync<MediaServiceInstanceHealthModelTableEntity>(mediaServiceName, MediaServiceInstanceHealthModelTableEntity.DefaultRowKeyValue).ConfigureAwait(false);

            // update datetime field to meet min requirements for dates
            eventDateTime = VerifyMinValue(eventDateTime);
            getResult.LastUpdated = eventDateTime;
            getResult.HealthState = instanceHealthState.ToString();

            var mergeResult = await this.tableStorageService.MergeAsync(getResult).ConfigureAwait(false);
            return mergeResult.GetMediaServiceInstanceHealthModel();
        }

        /// <summary>
        /// Ensures that DateTime fields are at least minDateTimeForTableStorage or later
        /// </summary>
        /// <param name="dateTime">DateTime field to check</param>
        /// <returns>Updated DateTime field</returns>
        private static DateTimeOffset VerifyMinValue(DateTimeOffset dateTime)
        {
            return dateTime > minDateTimeForTableStorage ? dateTime : minDateTimeForTableStorage;
        }
    }
}

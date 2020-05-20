namespace HighAvailability.Services
{
    using HighAvailability.Helpers;
    using HighAvailability.Models;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public class MediaServiceInstanceHealthStorageService : IMediaServiceInstanceHealthStorageService
    {
        private static DateTimeOffset minDateTimeForTableStorage = new DateTimeOffset(1900, 1, 1, 0, 0, 0, TimeSpan.FromSeconds(0));
        private readonly ITableStorageService tableStorageService;

        public MediaServiceInstanceHealthStorageService(ITableStorageService tableStorageService)
        {
            this.tableStorageService = tableStorageService ?? throw new ArgumentNullException(nameof(tableStorageService));
        }

        public async Task<MediaServiceInstanceHealthModel> CreateOrUpdateAsync(MediaServiceInstanceHealthModel mediaServiceInstanceHealthModel, ILogger logger)
        {
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

        public async Task<MediaServiceInstanceHealthModel> GetAsync(string mediaServiceName)
        {
            var model = await this.tableStorageService.GetAsync<MediaServiceInstanceHealthModelTableEntity>(mediaServiceName, MediaServiceInstanceHealthModelTableEntity.DefaultRowKeyValue).ConfigureAwait(false);
            return model.GetMediaServiceInstanceHealthModel();
        }

        public async Task<IEnumerable<MediaServiceInstanceHealthModel>> ListAsync()
        {
            return (await this.tableStorageService.ListAsync<MediaServiceInstanceHealthModelTableEntity>().ConfigureAwait(false)).Select(i => i.GetMediaServiceInstanceHealthModel());
        }

        public async Task<MediaServiceInstanceHealthModel> UpdateHealthStateAsync(string mediaServiceName, InstanceHealthState instanceHealthState, DateTimeOffset eventDateTime)
        {
            var getResult = await this.tableStorageService.GetAsync<MediaServiceInstanceHealthModelTableEntity>(mediaServiceName, MediaServiceInstanceHealthModelTableEntity.DefaultRowKeyValue).ConfigureAwait(false);

            eventDateTime = VerifyMinValue(eventDateTime);
            getResult.LastUpdated = eventDateTime;
            getResult.HealthState = instanceHealthState.ToString();

            var mergeResult = await this.tableStorageService.MergeAsync(getResult).ConfigureAwait(false);
            return mergeResult.GetMediaServiceInstanceHealthModel();
        }

        private static DateTimeOffset VerifyMinValue(DateTimeOffset dateTime)
        {
            return dateTime > minDateTimeForTableStorage ? dateTime : minDateTimeForTableStorage;
        }
    }
}

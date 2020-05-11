namespace HighAvailability.Services
{
    using HighAvailability.Helpers;
    using HighAvailability.Models;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public class MediaServiceInstanceHealthService : IMediaServiceInstanceHealthService
    {
        private readonly IMediaServiceInstanceHealthStorageService mediaServiceInstanceHealthStorageService;
        private readonly ILogger logger;

        public MediaServiceInstanceHealthService(IMediaServiceInstanceHealthStorageService mediaServiceInstanceHealthStorageService, ILogger logger)
        {
            this.mediaServiceInstanceHealthStorageService = mediaServiceInstanceHealthStorageService ?? throw new ArgumentNullException(nameof(mediaServiceInstanceHealthStorageService));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<MediaServiceInstanceHealthModel> CreateOrUpdateAsync(MediaServiceInstanceHealthModel mediaServiceInstanceHealthModel)
        {
            return await this.mediaServiceInstanceHealthStorageService.CreateOrUpdateAsync(mediaServiceInstanceHealthModel).ConfigureAwait(false);
        }

        public async Task<MediaServiceInstanceHealthModel> GetAsync(string mediaServiceName)
        {
            return await this.mediaServiceInstanceHealthStorageService.GetAsync(mediaServiceName).ConfigureAwait(false);
        }

        public async Task<bool> IsHealthyAsync(string mediaServiceName)
        {
            var mediaServiceInstanceHealthModel = await this.mediaServiceInstanceHealthStorageService.GetAsync(mediaServiceName).ConfigureAwait(false);
            return mediaServiceInstanceHealthModel.IsHealthy;
        }

        public async Task<IEnumerable<MediaServiceInstanceHealthModel>> ListAsync()
        {
            return await this.mediaServiceInstanceHealthStorageService.ListAsync().ConfigureAwait(false);
        }

        public async Task<IEnumerable<string>> ListHealthyAsync()
        {
            var result = (await this.mediaServiceInstanceHealthStorageService.ListAsync().ConfigureAwait(false)).Where(i => i.IsHealthy).Select(i => i.MediaServiceAccountName);
            this.logger.LogInformation($"MediaServiceInstanceHealthService::ListHealthyAsync: result={LogHelper.FormatObjectForLog(result)}");
            return result;
        }

        public async Task<IEnumerable<string>> ListUnHealthyAsync()
        {
            var result = (await this.mediaServiceInstanceHealthStorageService.ListAsync().ConfigureAwait(false)).Where(i => !i.IsHealthy).Select(i => i.MediaServiceAccountName);
            this.logger.LogInformation($"MediaServiceInstanceHealthService::ListUnHealthyAsync: result={LogHelper.FormatObjectForLog(result)}");
            return result;
        }

        public Task<IEnumerable<MediaServiceInstanceHealthModel>> ReEvaluateMediaServicesHealthAsync()
        {
            throw new NotImplementedException();
        }

        public async Task<MediaServiceInstanceHealthModel> UpdateHealthStateAsync(string mediaServiceName, bool isHealthy, DateTime eventDateTime)
        {
            return await this.mediaServiceInstanceHealthStorageService.UpdateHealthStateAsync(mediaServiceName, isHealthy, eventDateTime).ConfigureAwait(false);
        }

        public async Task<MediaServiceInstanceHealthModel> UpdateJobStateAsync(string mediaServiceName, bool isJobCompletedSuccessfully, DateTime eventDateTime)
        {
            return await this.mediaServiceInstanceHealthStorageService.UpdateProcessedJobStateAsync(mediaServiceName, isJobCompletedSuccessfully, eventDateTime).ConfigureAwait(false);
        }

        public async Task<MediaServiceInstanceHealthModel> UpdateSubmittedJobStateAsync(string mediaServiceName, DateTime eventDateTime)
        {
            return await this.mediaServiceInstanceHealthStorageService.UpdateSubmittedJobStateAsync(mediaServiceName, eventDateTime).ConfigureAwait(false);
        }
    }
}
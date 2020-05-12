namespace HighAvailability.Services
{
    using HighAvailability.Models;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IMediaServiceInstanceHealthStorageService
    {
        Task<MediaServiceInstanceHealthModel> CreateOrUpdateAsync(MediaServiceInstanceHealthModel mediaServiceInstanceHealthModel, ILogger logger);

        Task<MediaServiceInstanceHealthModel> GetAsync(string mediaServiceName);

        Task<IEnumerable<MediaServiceInstanceHealthModel>> ListAsync();

        Task<MediaServiceInstanceHealthModel> UpdateProcessedJobStateAsync(string mediaServiceName, bool isJobCompletedSuccessfully, DateTime eventDateTime);

        Task<MediaServiceInstanceHealthModel> UpdateSubmittedJobStateAsync(string mediaServiceName, DateTime eventDateTime);

        Task<MediaServiceInstanceHealthModel> UpdateHealthStateAsync(string mediaServiceName, bool isHealthy, DateTime eventDateTime);
    }
}

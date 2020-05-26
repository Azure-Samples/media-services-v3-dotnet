namespace HighAvailability.Interfaces
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

        Task<MediaServiceInstanceHealthModel> UpdateHealthStateAsync(string mediaServiceName, InstanceHealthState instanceHealthState, DateTimeOffset eventDateTime);
    }
}

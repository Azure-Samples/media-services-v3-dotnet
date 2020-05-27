namespace HighAvailability.Interfaces
{
    using HighAvailability.Models;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface to define methods to write and read Azure Media Services instance health data
    /// </summary>
    public interface IMediaServiceInstanceHealthStorageService
    {
        /// <summary>
        /// Stores Azure Media Services instance health data.
        /// </summary>
        /// <param name="mediaServiceInstanceHealthModel">Data to store</param>
        /// <param name="logger">Logger to log data</param>
        /// <returns></returns>
        Task<MediaServiceInstanceHealthModel> CreateOrUpdateAsync(MediaServiceInstanceHealthModel mediaServiceInstanceHealthModel, ILogger logger);

        /// <summary>
        /// Gets specific Azure Media Services instance health data record.
        /// </summary>
        /// <param name="mediaServiceName">Azure Media Services instance account name</param>
        /// <returns>Azure Media Services instance health data record</returns>
        Task<MediaServiceInstanceHealthModel> GetAsync(string mediaServiceName);

        /// <summary>
        /// Lists all available Azure Media Services instance health data records
        /// </summary>
        /// <returns>List of Azure Media Services instance health data records</returns>
        Task<IEnumerable<MediaServiceInstanceHealthModel>> ListAsync();

        /// <summary>
        /// Updates health state for the specific Azure Media Services instance health data record
        /// </summary>
        /// <param name="mediaServiceName">Azure Media Services instance account name</param>
        /// <param name="instanceHealthState">health state</param>
        /// <param name="eventDateTime">update record timestamp</param>
        /// <returns></returns>
        Task<MediaServiceInstanceHealthModel> UpdateHealthStateAsync(string mediaServiceName, InstanceHealthState instanceHealthState, DateTimeOffset eventDateTime);
    }
}

namespace HighAvailability.Interfaces
{
    using HighAvailability.Models;
    using Microsoft.Extensions.Logging;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface to define Azure Media Services instance health related methods
    /// </summary>
    public interface IMediaServiceInstanceHealthService
    {
        /// <summary>
        /// Records the fact that new job was submitted to a given Azure Media Services instance. 
        /// </summary>
        /// <param name="mediaServiceName">Azure Media Services instance account name.</param>
        /// <param name="logger">Logger to log data</param>
        void RecordInstanceUsage(string mediaServiceName, ILogger logger);

        /// <summary>
        /// Gets next available  Azure Media Services instance to process job.
        /// </summary>
        /// <param name="logger">Logger to log data</param>
        /// <returns>Azure Media Services instance account name</returns>
        Task<string> GetNextAvailableInstanceAsync(ILogger logger);

        /// <summary>
        /// Stores Azure Media Services instance health record
        /// </summary>
        /// <param name="mediaServiceInstanceHealthModel">Record to store</param>
        /// <param name="logger">Logger to log data</param>
        /// <returns></returns>
        Task<MediaServiceInstanceHealthModel> CreateOrUpdateAsync(MediaServiceInstanceHealthModel mediaServiceInstanceHealthModel, ILogger logger);

        /// <summary>
        /// Lists all the media services instances with associated health status 
        /// </summary>
        /// <returns>List of Azure Media Services instance health records</returns>
        Task<IEnumerable<MediaServiceInstanceHealthModel>> ListAsync();

        /// <summary>
        /// Recalculates health rating for each Azure Media Services instance health record
        /// </summary>
        /// <param name="logger">Logger to log data</param>
        /// <returns></returns>
        Task<IEnumerable<MediaServiceInstanceHealthModel>> ReEvaluateMediaServicesHealthAsync(ILogger logger);
    }
}

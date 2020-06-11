namespace HighAvailability.Interfaces
{
    using HighAvailability.Models;
    using Microsoft.Extensions.Logging;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface to define methods to store and load job requests
    /// </summary>
    interface IJobRequestStorageService
    {
        /// <summary>
        /// Stores a new job request
        /// </summary>
        /// <param name="jobRequestModel">Job request to store</param>
        /// <param name="logger">Logger to log data</param>
        /// <returns></returns>
        Task<JobRequestModel> CreateAsync(JobRequestModel jobRequestModel, ILogger logger);

        /// <summary>
        /// Gets next available job request.
        /// </summary>
        /// <param name="logger">Logger to log data</param>
        /// <returns>Job request from the storage</returns>
        Task<JobRequestModel> GetNextAsync(ILogger logger);
    }
}

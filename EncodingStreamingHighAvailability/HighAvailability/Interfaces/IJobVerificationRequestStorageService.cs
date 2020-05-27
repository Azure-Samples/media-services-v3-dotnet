namespace HighAvailability.Interfaces
{
    using HighAvailability.Models;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface to define methods to store and load job verification requests
    /// </summary>
    public interface IJobVerificationRequestStorageService
    {
        /// <summary>
        /// Create new job verification request. This requests is used to verify that job was successfully completed.
        /// </summary>
        /// <param name="jobVerificationRequestModel">Job verification request</param>
        /// <param name="verificationDelay">How far in future to run verification logic</param>
        /// <param name="logger">Logger to log data</param>
        /// <returns></returns>
        Task<JobVerificationRequestModel> CreateAsync(JobVerificationRequestModel jobVerificationRequestModel, TimeSpan verificationDelay, ILogger logger);

        /// <summary>
        /// Get next job verification request from the storage
        /// </summary>
        /// <param name="logger">Logger to log data</param>
        /// <returns>Loaded job verification request</returns>
        Task<JobVerificationRequestModel> GetNextAsync(ILogger logger);
    }
}

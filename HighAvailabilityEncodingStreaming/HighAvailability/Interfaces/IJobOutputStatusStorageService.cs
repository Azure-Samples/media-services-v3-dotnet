namespace HighAvailability.Interfaces
{
    using HighAvailability.Models;
    using Microsoft.Extensions.Logging;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface to define methods to write and read job output status event records.
    /// </summary>
    public interface IJobOutputStatusStorageService
    {
        /// <summary>
        /// Creates or updates job output status event record
        /// </summary>
        /// <param name="jobOutputStatusModel">Data to store</param>
        /// <param name="logger">Logger to log</param>
        /// <returns>Stored model</returns>
        Task<JobOutputStatusModel> CreateOrUpdateAsync(JobOutputStatusModel jobOutputStatusModel, ILogger logger);

        /// <summary>
        /// Reads all job output status records for a given job name and output asset name.
        /// </summary>
        /// <param name="jobName">Job name to load data for</param>
        /// <param name="jobOutputAssetName">Output asset name to load data for</param>
        /// <returns>List of job output status records</returns>
        Task<IEnumerable<JobOutputStatusModel>> ListAsync(string jobName, string jobOutputAssetName);

        /// <summary>
        /// Reads latest job output status record for a given job name and output asset name.
        /// </summary>
        /// <param name="jobName">Job name to load data for</param>
        /// <param name="jobOutputAssetName">Output asset name to load data for</param>
        /// <returns>Latest job output status record</returns>
        Task<JobOutputStatusModel> GetLatestJobOutputStatusAsync(string jobName, string jobOutputAssetName);

        /// <summary>
        /// Reads all records for a given account name and time duration condition
        /// </summary>
        /// <param name="mediaServiceAccountName">Account name to load data for</param>
        /// <param name="timeWindowInMinutesToLoadJobs">How far back to go to load data</param>
        /// <returns>List of job output status records</returns>
        Task<IEnumerable<JobOutputStatusModel>> ListByMediaServiceAccountNameAsync(string mediaServiceAccountName, int timeWindowInMinutesToLoadJobs);
    }
}

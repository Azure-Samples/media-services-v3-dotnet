namespace HighAvailability.Interfaces
{
    using HighAvailability.Models;
    using Microsoft.Extensions.Logging;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface to define methods to write and read Media Service call history.
    /// </summary>
    public interface IMediaServiceCallHistoryStorageService
    {
        /// <summary>
        /// Creates or updates Media Service call history record
        /// </summary>
        /// <param name="mediaServiceCallHistoryModel">Data to store</param>
        /// <param name="logger">Logger to log</param>
        /// <returns>Stored model</returns>
        Task<MediaServiceCallHistoryModel> CreateOrUpdateAsync(MediaServiceCallHistoryModel mediaServiceCallHistoryModel, ILogger logger);

        /// <summary>
        /// Reads all records for a given account name and time duration condition
        /// </summary>
        /// <param name="mediaServiceAccountName">Account name to load data for</param>
        /// <param name="timeWindowInMinutesToLoadData">How far back to go to load data</param>
        /// <returns>List of Media Service call history records</returns>
        Task<IEnumerable<MediaServiceCallHistoryModel>> ListByMediaServiceAccountNameAsync(string mediaServiceAccountName, int timeWindowInMinutesToLoadData);
    }
}

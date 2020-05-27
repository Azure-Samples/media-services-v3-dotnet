namespace HighAvailability.Interfaces
{
    using Microsoft.Extensions.Logging;
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface to define methods to sync job output status from Azure Media Services APIs.
    /// </summary>
    public interface IJobOutputStatusSyncService
    {
        /// <summary>
        /// EventGrid events sometimes are lost and manual resync is required. This method syncs job output status records between 
        /// job output status storage and Azure Media Services APIs. 
        /// </summary>
        /// <param name="currentTime">Current time, it is used to build time base criteria to load job status data.</param>
        /// <param name="logger">Logger to log data</param>
        /// <returns>Task for async operation</returns>
        Task SyncJobOutputStatusAsync(DateTime currentTime, ILogger logger);
    }
}
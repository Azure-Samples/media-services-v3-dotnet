namespace HighAvailability.Interfaces
{
    using HighAvailability.Models;
    using Microsoft.Extensions.Logging;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface to define methods to process job output status events
    /// </summary>
    public interface IJobOutputStatusService
    {
        /// <summary>
        /// Stores job output status record and submits request to provision encoded assets.
        /// </summary>
        /// <param name="jobOutputStatusModel">Input data model</param>
        /// <param name="logger">Logger to log data</param>
        /// <returns></returns>
        Task<JobOutputStatusModel> ProcessJobOutputStatusAsync(JobOutputStatusModel jobOutputStatusModel, ILogger logger);
    }
}

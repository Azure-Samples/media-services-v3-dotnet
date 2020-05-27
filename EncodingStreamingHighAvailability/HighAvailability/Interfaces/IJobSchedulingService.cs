namespace HighAvailability.Interfaces
{
    using HighAvailability.Models;
    using Microsoft.Azure.Management.Media.Models;
    using Microsoft.Extensions.Logging;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface to define methods to submit jobs to Azure Media Service
    /// </summary>
    public interface IJobSchedulingService
    {
        /// <summary>
        /// Submits job to Azure Media Services.
        /// </summary>
        /// <param name="jobRequestModel">Job to submit.</param>
        /// <param name="logger">Logger to log data</param>
        /// <returns>Submitted job</returns>
        Task<Job> SubmitJobAsync(JobRequestModel jobRequestModel, ILogger logger);
    }
}

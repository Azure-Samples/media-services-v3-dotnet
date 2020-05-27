namespace HighAvailability.Interfaces
{
    using HighAvailability.Models;
    using Microsoft.Extensions.Logging;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface to define methods to verify that jobs are completed successfully
    /// </summary>
    public interface IJobVerificationService
    {
        /// <summary>
        /// Verifies the status of given job, implements business logic to resubmit jobs if needed
        /// </summary>
        /// <param name="jobVerificationRequestModel">Job verification request</param>
        /// <param name="logger">Logger to log data</param>
        /// <returns></returns>
        Task<JobVerificationRequestModel> VerifyJobAsync(JobVerificationRequestModel jobVerificationRequestModel, ILogger logger);
    }
}

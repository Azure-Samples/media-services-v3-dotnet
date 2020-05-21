namespace HighAvailability.Services
{
    using HighAvailability.Models;
    using Microsoft.Azure.Management.Media.Models;
    using Microsoft.Extensions.Logging;
    using System.Threading.Tasks;

    public interface IJobSchedulingService
    {
        // This is the main function to submit jobs
        // It should use IMediaServiceInstanceHealthService to determine what media service account to use to submit this job. 
        // It could also use IJobOutputStatusStorageService to determine number of outstanding jobs before submitting new one
        Task<Job> SubmitJobAsync(JobRequestModel jobRequestModel, ILogger logger);
    }
}

namespace media_services_high_availability_shared.Services
{
    using media_services_high_availability_shared.Models;
    using Microsoft.Azure.Management.Media.Models;
    using System.Threading.Tasks;

    interface IJobSchedulerService
    {
        // This is the main function to submit jobs
        // It should use IMediaServiceInstanceHealthService to determine what media service account to use to submit this job. 
        // It could also use IJobStatusStorageService to determine number of outstanding jobs before submitting new one
        Task<Job> SubmitJobAsync(JobRequestModel jobRequestModel);
    }
}

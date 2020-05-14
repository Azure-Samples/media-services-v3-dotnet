namespace HighAvailability.Services
{
    using HighAvailability.Models;
    using Microsoft.Extensions.Logging;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IJobStatusStorageService
    {
        // add or update new job status 
        Task<JobStatusModel> CreateOrUpdateAsync(JobStatusModel jobStatusModel, ILogger logger);

        // Load status by Id
        Task<JobStatusModel> GetAsync(string id);   // we may not need this one

        // List all the job statuses for a given job
        Task<IEnumerable<JobStatusModel>> ListAsync(string jobName);

        // This may result in very long list, we may need to paginate or have a way to filter down results
        // For example, return status for the last 100 or so jobs only, that should be enough to determine the health of the endpoint
        Task<IEnumerable<JobStatusModel>> ListAsync();

        // Load latest status for a given job 
        Task<JobStatusModel> GetLatestJobStatusAsync(string jobName);

        Task<IEnumerable<JobStatusModel>> ListByMediaServiceAccountNameAsync(string mediaServiceAccountName);
    }
}

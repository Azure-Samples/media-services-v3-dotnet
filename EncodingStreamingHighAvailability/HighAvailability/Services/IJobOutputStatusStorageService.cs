namespace HighAvailability.Services
{
    using HighAvailability.Models;
    using Microsoft.Extensions.Logging;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IJobOutputStatusStorageService
    {
        // add or update new job status 
        Task<JobOutputStatusModel> CreateOrUpdateAsync(JobOutputStatusModel jobOutputStatusModel, ILogger logger);

        // List all the job statuses for a given job
        Task<IEnumerable<JobOutputStatusModel>> ListAsync(string jobName, string jobOutputAssetName);

        // This may result in very long list, we may need to paginate or have a way to filter down results
        // For example, return status for the last 100 or so jobs only, that should be enough to determine the health of the endpoint
        Task<IEnumerable<JobOutputStatusModel>> ListAsync();

        // Load latest status for a given job 
        Task<JobOutputStatusModel> GetLatestJobOutputStatusAsync(string jobName, string jobOutputAssetName);

        Task<IEnumerable<JobOutputStatusModel>> ListByMediaServiceAccountNameAsync(string mediaServiceAccountName, int timeWindowInMinutesToLoadJobs);
    }
}

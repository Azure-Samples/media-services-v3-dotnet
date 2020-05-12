namespace HighAvailability.Services
{
    using HighAvailability.Models;
    using Microsoft.Extensions.Logging;
    using System.Threading.Tasks;

    public interface IJobStatusService
    {
        Task<JobStatusModel> ProcessJobStatusAsync(JobStatusModel jobStatusModel, ILogger logger);
    }
}

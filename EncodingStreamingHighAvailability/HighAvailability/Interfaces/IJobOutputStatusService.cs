namespace HighAvailability.Interfaces
{
    using HighAvailability.Models;
    using Microsoft.Extensions.Logging;
    using System.Threading.Tasks;

    public interface IJobOutputStatusService
    {
        Task<JobOutputStatusModel> ProcessJobOutputStatusAsync(JobOutputStatusModel jobOutputStatusModel, ILogger logger);
    }
}

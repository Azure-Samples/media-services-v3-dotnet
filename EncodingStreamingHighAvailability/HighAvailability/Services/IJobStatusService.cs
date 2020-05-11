namespace HighAvailability.Services
{
    using HighAvailability.Models;
    using System.Threading.Tasks;

    public interface IJobStatusService
    {
        Task<JobStatusModel> ProcessJobStatusAsync(JobStatusModel jobStatusModel);
    }
}

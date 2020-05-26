namespace HighAvailability.Interfaces
{
    using HighAvailability.Models;
    using Microsoft.Extensions.Logging;
    using System.Threading.Tasks;

    public interface IJobVerificationService
    {
        Task<JobVerificationRequestModel> VerifyJobAsync(JobVerificationRequestModel jobVerificationRequestModel, ILogger logger);
    }
}

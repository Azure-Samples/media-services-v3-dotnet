namespace HighAvailability.Services
{
    using HighAvailability.Models;
    using System.Threading.Tasks;

    public interface IJobVerificationService
    {
        Task<JobVerificationRequestModel> VerifyJobAsync(JobVerificationRequestModel jobVerificationRequestModel);
    }
}

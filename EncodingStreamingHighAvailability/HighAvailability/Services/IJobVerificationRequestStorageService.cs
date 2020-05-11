namespace HighAvailability.Services
{
    using HighAvailability.Models;
    using System;
    using System.Threading.Tasks;

    public interface IJobVerificationRequestStorageService
    {
        // Persist new request to verify job in the future
        Task<JobVerificationRequestModel> CreateAsync(JobVerificationRequestModel jobVerificationRequestModel, TimeSpan verificationDelay);

        // Get next verification request
        Task<JobVerificationRequestModel?> GetNextAsync();
    }
}

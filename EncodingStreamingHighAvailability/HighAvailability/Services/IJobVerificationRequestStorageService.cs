namespace HighAvailability.Services
{
    using HighAvailability.Models;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Threading.Tasks;

    public interface IJobVerificationRequestStorageService
    {
        // Persist new request to verify job in the future
        Task<JobVerificationRequestModel> CreateAsync(JobVerificationRequestModel jobVerificationRequestModel, TimeSpan verificationDelay, ILogger logger);

        // Get next verification request
        Task<JobVerificationRequestModel> GetNextAsync(ILogger logger);
    }
}

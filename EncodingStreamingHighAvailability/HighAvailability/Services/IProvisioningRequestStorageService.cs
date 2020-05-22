namespace HighAvailability.Services
{
    using HighAvailability.Models;
    using Microsoft.Extensions.Logging;
    using System.Threading.Tasks;

    public interface IProvisioningRequestStorageService
    {
        Task<ProvisioningRequestModel> CreateAsync(ProvisioningRequestModel provisioningRequest, ILogger logger);
        Task<ProvisioningRequestModel> GetNextAsync(ILogger logger);
    }
}

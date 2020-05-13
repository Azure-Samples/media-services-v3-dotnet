namespace HighAvailability.Services
{
    using HighAvailability.Models;
    using Microsoft.Extensions.Logging;
    using System.Threading.Tasks;

    public interface IStreamProvisioningRequestStorageService
    {
        Task<StreamProvisioningRequestModel> CreateAsync(StreamProvisioningRequestModel streamProvisioningRequest, ILogger logger);
        Task<StreamProvisioningRequestModel> GetNextAsync(ILogger logger);
    }
}

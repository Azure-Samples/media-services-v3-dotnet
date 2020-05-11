namespace HighAvailability.Services
{
    using HighAvailability.Models;
    using System.Threading.Tasks;

    public interface IStreamProvisioningRequestStorageService
    {
        Task<StreamProvisioningRequestModel> CreateAsync(StreamProvisioningRequestModel streamProvisioningRequest);
        Task<StreamProvisioningRequestModel?> GetNextAsync();
    }
}

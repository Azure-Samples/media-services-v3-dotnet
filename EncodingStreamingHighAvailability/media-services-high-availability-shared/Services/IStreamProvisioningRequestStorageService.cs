namespace media_services_high_availability_shared.Services
{
    using media_services_high_availability_shared.Models;
    using System.Threading.Tasks;

    public interface IStreamProvisioningRequestStorageService
    {
        Task<StreamProvisioningRequestModel> CreateAsync(StreamProvisioningRequestModel streamProvisioningRequest);
        Task<StreamProvisioningRequestModel?> GetNextAsync();
    }
}

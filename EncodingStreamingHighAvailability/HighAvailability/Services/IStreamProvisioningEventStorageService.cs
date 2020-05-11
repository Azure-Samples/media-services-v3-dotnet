namespace HighAvailability.Services
{
    using HighAvailability.Models;
    using System.Threading.Tasks;

    public interface IStreamProvisioningEventStorageService
    {
        Task<StreamProvisioningEventModel> CreateAsync(StreamProvisioningEventModel streamProvisioningEventModel);
        Task<StreamProvisioningEventModel?> GetNextAsync();
    }
}

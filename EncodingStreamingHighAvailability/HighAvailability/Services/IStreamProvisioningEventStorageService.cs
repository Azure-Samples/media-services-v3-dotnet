namespace HighAvailability.Services
{
    using HighAvailability.Models;
    using Microsoft.Extensions.Logging;
    using System.Threading.Tasks;

    public interface IStreamProvisioningEventStorageService
    {
        Task<StreamProvisioningEventModel> CreateAsync(StreamProvisioningEventModel streamProvisioningEventModel, ILogger logger);
        Task<StreamProvisioningEventModel?> GetNextAsync(ILogger logger);
    }
}

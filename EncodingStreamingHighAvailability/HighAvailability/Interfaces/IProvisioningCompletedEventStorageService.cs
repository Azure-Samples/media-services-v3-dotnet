namespace HighAvailability.Interfaces
{
    using HighAvailability.Models;
    using Microsoft.Extensions.Logging;
    using System.Threading.Tasks;

    public interface IProvisioningCompletedEventStorageService
    {
        Task<ProvisioningCompletedEventModel> CreateAsync(ProvisioningCompletedEventModel provisioningCompletedEventModel, ILogger logger);
        Task<ProvisioningCompletedEventModel> GetNextAsync(ILogger logger);
    }
}

namespace HighAvailability.Services
{
    using HighAvailability.Models;
    using Microsoft.Extensions.Logging;
    using System.Threading.Tasks;

    public interface IProvisioningService
    {
        Task ProvisionAsync(ProvisioningRequestModel provisioningRequestModel, ILogger logger);
    }
}

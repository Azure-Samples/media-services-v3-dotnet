namespace HighAvailability.Interfaces
{
    using HighAvailability.Models;
    using Microsoft.Extensions.Logging;
    using System.Threading.Tasks;

    public interface IProvisioningOrchestrator
    {
        Task ProvisionAsync(ProvisioningRequestModel provisioningRequestModel, ILogger logger);
    }
}

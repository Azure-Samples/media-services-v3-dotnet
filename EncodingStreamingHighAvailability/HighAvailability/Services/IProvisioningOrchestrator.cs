namespace HighAvailability.Services
{
    using HighAvailability.Models;
    using Microsoft.Extensions.Logging;
    using System.Threading.Tasks;

    public interface IProvisioningOrchestrator
    {
        Task ProvisionAsync(StreamProvisioningRequestModel request, ILogger logger);
    }
}

namespace HighAvailability.Services
{
    using HighAvailability.Models;
    using Microsoft.Extensions.Logging;
    using System.Threading.Tasks;

    public interface IProvisioningService
    {
        Task ProvisionAsync(StreamProvisioningRequestModel request, ILogger logger);
    }
}

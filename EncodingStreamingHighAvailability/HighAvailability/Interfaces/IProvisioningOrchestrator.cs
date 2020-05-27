namespace HighAvailability.Interfaces
{
    using HighAvailability.Models;
    using Microsoft.Extensions.Logging;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface to define methods to orchestrate encoded assets provisioning logic.
    /// </summary>
    public interface IProvisioningOrchestrator
    {
        /// <summary>
        /// Provisions encoded assets
        /// </summary>
        /// <param name="provisioningRequestModel">Request to process</param>
        /// <param name="logger">Logger to log data</param>
        /// <returns></returns>
        Task ProvisionAsync(ProvisioningRequestModel provisioningRequestModel, ILogger logger);
    }
}

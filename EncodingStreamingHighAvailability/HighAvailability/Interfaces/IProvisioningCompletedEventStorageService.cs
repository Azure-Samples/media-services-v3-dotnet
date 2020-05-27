namespace HighAvailability.Interfaces
{
    using HighAvailability.Models;
    using Microsoft.Extensions.Logging;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface to define methods to store and load provisioning completed events
    /// </summary>
    public interface IProvisioningCompletedEventStorageService
    {
        /// <summary>
        /// Stores provisioning completed event.
        /// </summary>
        /// <param name="provisioningCompletedEventModel">Event to store</param>
        /// <param name="logger">Logger to log data</param>
        /// <returns></returns>
        Task<ProvisioningCompletedEventModel> CreateAsync(ProvisioningCompletedEventModel provisioningCompletedEventModel, ILogger logger);

        /// <summary>
        /// Gets next provisioning completed event from storage
        /// </summary>
        /// <param name="logger"></param>
        /// <returns>Provisioning completed event</returns>
        Task<ProvisioningCompletedEventModel> GetNextAsync(ILogger logger);
    }
}

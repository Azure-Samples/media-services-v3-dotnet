namespace HighAvailability.Interfaces
{
    using Microsoft.Azure.Management.Media;
    using Microsoft.Extensions.Logging;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface for creating IAzureMediaServicesClient instances
    /// </summary>
    public interface IMediaServiceInstanceFactory
    {
        /// <summary>
        /// Gets/Creates IAzureMediaServicesClient instances.
        /// </summary>
        /// <param name="accountName">Account name for this request</param>
        /// <returns>Azure Media Services instance client</returns>
        IAzureMediaServicesClient GetMediaServiceInstance(string accountName, ILogger logger);

        /// <summary>
        /// Resets Media Service client. This should be used when error happens and new client connection is required.
        /// </summary>
        void ResetMediaServiceInstance();
    }
}

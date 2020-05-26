namespace HighAvailability.Interfaces
{
    using HighAvailability.Models;
    using Microsoft.Extensions.Logging;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// This is the main service to implement logic to determine what media service instances are healthy or unhealthy
    /// </summary>
    public interface IMediaServiceInstanceHealthService
    {
        void RecordInstanceUsage(string mediaServiceName, ILogger logger);

        Task<string> GetNextAvailableInstanceAsync(ILogger logger);

        Task<MediaServiceInstanceHealthModel> CreateOrUpdateAsync(MediaServiceInstanceHealthModel mediaServiceInstanceHealthModel, ILogger logger);

        // List all the media services instances with associated health status        
        Task<IEnumerable<MediaServiceInstanceHealthModel>> ListAsync();

        // This method should implement all the logic how to determine if given instance is healhy or not
        Task<IEnumerable<MediaServiceInstanceHealthModel>> ReEvaluateMediaServicesHealthAsync(ILogger logger);
    }
}

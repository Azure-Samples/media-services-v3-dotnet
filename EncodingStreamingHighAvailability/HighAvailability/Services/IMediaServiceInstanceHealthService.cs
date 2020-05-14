namespace HighAvailability.Services
{
    using HighAvailability.Models;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// This is the main service to implement logic to determine what media service instances are healthy or unhealthy
    /// </summary>
    public interface IMediaServiceInstanceHealthService
    {
        // Check if specific media service instance is healthy
        Task<InstanceHealthState> GetHealthStateAsync(string mediaServiceName);

        // List all healthy instances
        Task<IEnumerable<string>> ListHealthyAsync(ILogger logger);

        // List all unhealthy instances
        Task<IEnumerable<string>> ListUnHealthyAsync(ILogger logger);

        Task<MediaServiceInstanceHealthModel> CreateOrUpdateAsync(MediaServiceInstanceHealthModel mediaServiceInstanceHealthModel, ILogger logger);

        // List all the media services instances with associated health status        
        Task<IEnumerable<MediaServiceInstanceHealthModel>> ListAsync();

        // Get health info for specific media services instance
        Task<MediaServiceInstanceHealthModel> GetAsync(string mediaServiceName);

        // This method should implement all the logic how to determine if given instance is healhy or not
        Task<IEnumerable<MediaServiceInstanceHealthModel>> ReEvaluateMediaServicesHealthAsync();

        Task<MediaServiceInstanceHealthModel> UpdateHealthStateAsync(string mediaServiceName, InstanceHealthState instanceHealthState, DateTimeOffset eventDateTime);
    }
}

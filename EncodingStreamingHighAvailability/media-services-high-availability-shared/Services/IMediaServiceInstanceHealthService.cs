namespace media_services_high_availability_shared.Services
{
    using media_services_high_availability_shared.Models;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// This is the main service to implement logic to determine what media service instances are healthy or unhealthy
    /// </summary>
    public interface IMediaServiceInstanceHealthService
    {
        // Check if specific media service instance is healthy
        Task<bool> IsHealthyAsync(string mediaServiceName);

        // List all healthy instances
        Task<IEnumerable<string>> ListHealthyAsync();

        // List all unhealthy instances
        Task<IEnumerable<string>> ListUnHealthyAsync();

        Task<MediaServiceInstanceHealthModel> CreateOrUpdateAsync(MediaServiceInstanceHealthModel mediaServiceInstanceHealthModel);

        // List all the media services instances with associated health status        
        Task<IEnumerable<MediaServiceInstanceHealthModel>> ListAsync();

        // Get health info for specific media services instance
        Task<MediaServiceInstanceHealthModel> GetAsync(string mediaServiceName);

        // This should persist new information. TBD if this should trigger recalculation of instance health
        Task<MediaServiceInstanceHealthModel> UpdateJobStateAsync(string mediaServiceName, bool isJobCompletedSuccessfully, DateTime eventDateTime);

        Task<MediaServiceInstanceHealthModel> UpdateSubmittedJobStateAsync(string mediaServiceName, DateTime eventDateTime);

        // This method should implement all the logic how to determine if given instance is healhy or not
        Task<IEnumerable<MediaServiceInstanceHealthModel>> ReEvaluateMediaServicesHealthAsync();

        Task<MediaServiceInstanceHealthModel> UpdateHealthStateAsync(string mediaServiceName, bool isHealthy, DateTime eventDateTime);
    }
}

namespace media_services_high_availability_shared.Services
{
    using media_services_high_availability_shared.Models;
    using System.Threading.Tasks;

    interface IJobRequestStorageService
    {
        // Creates a request to submit a new job
        Task<JobRequestModel> CreateAsync(JobRequestModel jobRequestModel);

        // Get next request to process. If AF is triggered directly by message on the queue, this method may not be used. 
        // TBD if this is ok to do or if we want to do some kind of abstraction for AF trigger logic
        Task<JobRequestModel?> GetNextAsync();
    }
}

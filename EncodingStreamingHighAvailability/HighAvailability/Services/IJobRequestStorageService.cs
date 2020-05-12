﻿namespace HighAvailability.Services
{
    using HighAvailability.Models;
    using Microsoft.Extensions.Logging;
    using System.Threading.Tasks;

    interface IJobRequestStorageService
    {
        // Creates a request to submit a new job
        Task<JobRequestModel> CreateAsync(JobRequestModel jobRequestModel, ILogger logger);

        // Get next request to process. If AF is triggered directly by message on the queue, this method may not be used. 
        // TBD if this is ok to do or if we want to do some kind of abstraction for AF trigger logic
        Task<JobRequestModel?> GetNextAsync(ILogger logger);
    }
}

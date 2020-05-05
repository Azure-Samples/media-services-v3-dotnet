﻿using media_services_high_availability_shared.Models;
using Microsoft.Azure.Management.Media.Models;
using System.Threading.Tasks;

namespace media_services_high_availability_shared.Services
{
    interface IJobRerunService
    {
        // This function should implement logic if job needs to be resubmited or if more wait is needed
        // It will have to reference IJobStatusStorageService to check current state of the job and determine next action
        // For resubmit logic, it should take depency on IMediaServiceInstanceHealthService to determine media service to submit the job
        Task<JobState> VerifyAsync(JobVerificationRequestModel jobVerificationRequestModel);
    }
}

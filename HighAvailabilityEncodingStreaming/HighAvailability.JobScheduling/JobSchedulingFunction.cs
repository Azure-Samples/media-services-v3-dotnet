// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace HighAvailability.JobScheduling
{
    using HighAvailability.Helpers;
    using HighAvailability.Interfaces;
    using HighAvailability.Models;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Implements job scheduling Azure function. It is triggered by messages in job-requests Azure queue.
    /// This module submits new jobs to Media Services cluster. It tracks health status of each Media Services instance and determines next healthy instance to submit a new job.
    /// See TBD link for more details
    /// </summary>
    public class JobSchedulingFunction
    {
        /// <summary>
        /// Json settings to deserialize data using full type names. 
        /// </summary>
        private static readonly JsonSerializerSettings jsonSettings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };

        /// <summary>
        /// Service to process job scheduling requests.
        /// </summary>
        private IJobSchedulingService jobSchedulingService { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="jobSchedulingService">Service to process job scheduling requests</param>
        public JobSchedulingFunction(IJobSchedulingService jobSchedulingService)
        {
            this.jobSchedulingService = jobSchedulingService ?? throw new ArgumentNullException(nameof(jobSchedulingService));
        }

        /// <summary>
        /// This function is triggered by messages in job-requests Azure queue.
        /// It submits a new job request to AMS cluster.
        /// If function fails, exception is thrown and message is automatically reprocessed up to 5 times by default.
        /// See this link for more details how to configure retry settings
        /// https://docs.microsoft.com/en-us/azure/azure-functions/functions-bindings-storage-queue-output?tabs=csharp#hostjson-settings
        /// </summary>
        /// <param name="message">Request data</param>
        /// <param name="logger">Logger to log data to App Insights</param>
        /// <returns></returns>
        [FunctionName("JobSchedulingFunction")]
        public async Task Run([QueueTrigger("job-requests", Connection = "StorageAccountConnectionString")] string message, ILogger logger)
        {
            try
            {
                logger.LogInformation($"JobSchedulingFunction::Run triggered, message={message}");

                // Request model uses inheritance, specific json deserialization settings have to be used to correctly deserialize data
                var jobRequestModel = JsonConvert.DeserializeObject<JobRequestModel>(message, jsonSettings);
                var result = await this.jobSchedulingService.SubmitJobAsync(jobRequestModel, logger).ConfigureAwait(false);

                logger.LogInformation($"JobSchedulingFunction::Run completed, result={LogHelper.FormatObjectForLog(result)}");
            }
            catch (Exception e)
            {
                logger.LogError($"JobSchedulingFunction::Run failed, exception={e.Message} message={message}");
                // in case of error, exception is thrown in order to trigger reprocess. 
                throw;
            }
        }
    }
}

namespace HighAvailability.JobVerification
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
    /// Implements job verification Azure function. It is triggered by messages in job-verification-requests Azure queue
    /// </summary>
    public class JobVerificationFunction
    {
        /// <summary>
        /// Json settings to deserialize data using full type names. 
        /// </summary>
        private static readonly JsonSerializerSettings jsonSettings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };

        /// <summary>
        /// Service to process job verification requests
        /// </summary>
        private IJobVerificationService jobVerificationService { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="jobVerificationService">Service to process job verification requests</param>
        public JobVerificationFunction(IJobVerificationService jobVerificationService)
        {
            this.jobVerificationService = jobVerificationService ?? throw new ArgumentNullException(nameof(jobVerificationService));
        }

        /// <summary>
        /// This function is triggered by messages in job-verification-requests Azure queue.
        /// It runs job verification logic to verify that jobs are completed successfully or resubmit failed ones.
        /// If function fails, exception is thrown and message is automatically reprocessed up to 5 times by default.
        /// See this link for more details how to configure retry settings
        /// https://docs.microsoft.com/en-us/azure/azure-functions/functions-bindings-storage-queue-output?tabs=csharp#hostjson-settings
        /// </summary>
        /// <param name="message">Message data</param>
        /// <param name="logger">Logger to log data to App Insights</param>
        /// <returns></returns>
        [FunctionName("JobVerificationFunction")]
        public async Task Run([QueueTrigger("job-verification-requests", Connection = "StorageAccountConnectionString")] string message, ILogger logger)
        {
            try
            {
                logger.LogInformation($"JobVerificationFunction::Run triggered, message={message}");

                // Request model uses inheritance, specific json deserialization settings have to be used to correctly deserialize data
                var jobVerificationRequestModel = JsonConvert.DeserializeObject<JobVerificationRequestModel>(message, jsonSettings);

                var result = await this.jobVerificationService.VerifyJobAsync(jobVerificationRequestModel, logger).ConfigureAwait(false);

                logger.LogInformation($"JobVerificationFunction::Run completed, result={LogHelper.FormatObjectForLog(result)}");
            }
            catch (Exception e)
            {
                logger.LogError($"JobVerificationFunction::Run failed: exception={e.Message} message={message}");
                // in case of error, exception is thrown in order to trigger reprocess. 
                throw;
            }
        }
    }
}

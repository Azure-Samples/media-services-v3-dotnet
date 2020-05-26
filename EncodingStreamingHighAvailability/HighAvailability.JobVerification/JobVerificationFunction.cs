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

    public class JobVerificationFunction
    {
        private static readonly JsonSerializerSettings jsonSettings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };

        private IJobVerificationService jobVerificationService { get; set; }

        public JobVerificationFunction(IJobVerificationService jobVerificationService)
        {
            this.jobVerificationService = jobVerificationService ?? throw new ArgumentNullException(nameof(jobVerificationService));
        }

        [FunctionName("JobVerificationFunction")]
        public async Task Run([QueueTrigger("job-verification-requests", Connection = "StorageAccountConnectionString")] string message, ILogger logger)
        {
            try
            {
                logger.LogInformation($"JobVerificationFunction::Run triggered, message={message}");

                var jobVerificationRequestModel = JsonConvert.DeserializeObject<JobVerificationRequestModel>(message, jsonSettings);
                var result = await this.jobVerificationService.VerifyJobAsync(jobVerificationRequestModel, logger).ConfigureAwait(false);

                logger.LogInformation($"JobVerificationFunction::Run completed, result={LogHelper.FormatObjectForLog(result)}");
            }
            catch (Exception e)
            {
                logger.LogError($"JobVerificationFunction::Run failed: exception={e.Message} message={message}");
                throw;
            }
        }
    }
}

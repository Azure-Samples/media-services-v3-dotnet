namespace HighAvailability.JobScheduling
{
    using HighAvailability.Helpers;
    using HighAvailability.Models;
    using HighAvailability.Services;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using System;
    using System.Threading.Tasks;

    public class JobSchedulingFunction
    {
        private static readonly JsonSerializerSettings jsonSettings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };

        private IJobSchedulingService jobSchedulingService { get; set; }

        public JobSchedulingFunction(IJobSchedulingService jobSchedulingService)
        {
            this.jobSchedulingService = jobSchedulingService ?? throw new ArgumentNullException(nameof(jobSchedulingService));
        }


        [FunctionName("JobSchedulingFunction")]
        public async Task Run([QueueTrigger("job-requests", Connection = "StorageAccountConnectionString")] string message, ILogger logger)
        {
            try
            {
                logger.LogInformation($"JobSchedulingFunction::Run triggered, message={message}");

                var jobRequestModel = JsonConvert.DeserializeObject<JobRequestModel>(message, jsonSettings);
                var result = await this.jobSchedulingService.SubmitJobAsync(jobRequestModel, logger).ConfigureAwait(false);

                logger.LogInformation($"JobSchedulingFunction::Run completed, result={LogHelper.FormatObjectForLog(result)}");
            }
            catch (Exception e)
            {
                logger.LogError($"JobSchedulingFunction::Run failed, exception={e.Message} message={message}");
                throw;
            }
        }
    }
}

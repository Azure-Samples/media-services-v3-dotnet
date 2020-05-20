namespace HighAvailability.JobScheduler
{
    using HighAvailability.Helpers;
    using HighAvailability.Models;
    using HighAvailability.Services;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using System;
    using System.Threading.Tasks;

    public class JobSchedulerFunction
    {
        private static readonly JsonSerializerSettings jsonSettings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };

        private IJobSchedulerService jobSchedulerService { get; set; }

        public JobSchedulerFunction(IJobSchedulerService jobSchedulerService)
        {
            this.jobSchedulerService = jobSchedulerService ?? throw new ArgumentNullException(nameof(jobSchedulerService));
        }


        [FunctionName("JobSchedulerFunction")]
        public async Task Run([QueueTrigger("job-requests", Connection = "StorageAccountConnectionString")] string message, ILogger logger)
        {
            try
            {
                logger.LogInformation($"JobSchedulerFunction::Run triggered, message={message}");

                var jobRequestModel = JsonConvert.DeserializeObject<JobRequestModel>(message, jsonSettings);
                var result = await this.jobSchedulerService.SubmitJobAsync(jobRequestModel, logger).ConfigureAwait(false);

                logger.LogInformation($"JobSchedulerFunction::Run completed, result={LogHelper.FormatObjectForLog(result)}");
            }
            catch (Exception e)
            {
                logger.LogError($"JobSchedulerFunction::Run failed, exception={e.Message} message={message}");
                throw;
            }
        }
    }
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace HighAvailabikity.Provisioner
{
    using HighAvailability.Interfaces;
    using HighAvailability.Models;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Implements provisioning Azure function. It is triggered by messages in provisioning-requests Azure queue.
    /// This module provisions processed assets. It copies asset data to all AMS instances and sets up Azure Front Door service 
    /// to ensure that assets can be streamed even when some Media Services instances are not available. It also sets up streaming locators. 
    /// It is easy to remove or add provisioning steps to address specific business scenario.
    /// See README.md for more details.
    /// </summary>
    public class ProvisioningFunction
    {
        /// <summary>
        /// Service to orchestrate provisioning
        /// </summary>
        private IProvisioningOrchestrator provisioningOrchestrator { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="provisioningOrchestrator">Service to orchestrate provisioning</param>
        public ProvisioningFunction(IProvisioningOrchestrator provisioningOrchestrator)
        {
            this.provisioningOrchestrator = provisioningOrchestrator ?? throw new ArgumentNullException(nameof(provisioningOrchestrator));
        }

        /// <summary>
        /// This function is triggered by messages in provisioning-requests Azure queue.
        /// It runs provisioning logic for processed assets.
        /// If function fails, exception is thrown and message is automatically reprocessed up to 5 times by default.
        /// See this link for more details how to configure retry settings
        /// https://docs.microsoft.com/en-us/azure/azure-functions/functions-bindings-storage-queue-output?tabs=csharp#hostjson-settings
        /// </summary>
        /// <param name="message"></param>
        /// <param name="logger"></param>
        /// <returns></returns>
        [FunctionName("ProvisioningFunction")]
        public async Task Run([QueueTrigger("provisioning-requests", Connection = "StorageAccountConnectionString")] string message, ILogger logger)
        {
            try
            {
                logger.LogInformation($"ProvisioningFunction::Run triggered, message={message}");

                var provisioningRequestModel = JsonConvert.DeserializeObject<ProvisioningRequestModel>(message);
                await this.provisioningOrchestrator.ProvisionAsync(provisioningRequestModel, logger).ConfigureAwait(false);

                logger.LogInformation($"ProvisioningFunction::Run completed, message={message}");
            }
            catch (Exception e)
            {
                logger.LogError($"ProvisioningFunction::Run failed: exception={e.Message} message={message}");
                throw;
            }
        }
    }
}

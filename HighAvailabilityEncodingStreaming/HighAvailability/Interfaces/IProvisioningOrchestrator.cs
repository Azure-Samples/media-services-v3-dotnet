// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace HighAvailability.Interfaces
{
    using HighAvailability.Models;
    using Microsoft.Extensions.Logging;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface to define methods to orchestrate processed assets provisioning logic.
    /// </summary>
    public interface IProvisioningOrchestrator
    {
        /// <summary>
        /// Provisions processed assets
        /// </summary>
        /// <param name="provisioningRequestModel">Request to process</param>
        /// <param name="logger">Logger to log data</param>
        /// <returns>Task for async operation</returns>
        Task ProvisionAsync(ProvisioningRequestModel provisioningRequestModel, ILogger logger);
    }
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace HighAvailability.Interfaces
{
    using HighAvailability.Models;
    using Microsoft.Extensions.Logging;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface to define service methods to provision processed assets
    /// </summary>
    public interface IProvisioningService
    {
        /// <summary>
        /// Provisions processed assets
        /// </summary>
        /// <param name="provisioningRequestModel">Model to provision</param>
        /// <param name="provisioningCompletedEventModel">Provision completed event model to store provisioning data</param>
        /// <param name="logger">Logger to log data</param>
        /// <returns></returns>
        Task ProvisionAsync(ProvisioningRequestModel provisioningRequestModel, ProvisioningCompletedEventModel provisioningCompletedEventModel, ILogger logger);
    }
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace HighAvailability.Interfaces
{
    using HighAvailability.Models;
    using Microsoft.Extensions.Logging;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface to define methods to store and load provisioning requests
    /// </summary>
    public interface IProvisioningRequestStorageService
    {
        /// <summary>
        /// Stores new provisioning request
        /// </summary>
        /// <param name="provisioningRequest">Request to store</param>
        /// <param name="logger">Logger to log data</param>
        /// <returns>Stored provisioning request</returns>
        Task<ProvisioningRequestModel> CreateAsync(ProvisioningRequestModel provisioningRequest, ILogger logger);

        /// <summary>
        /// Gets next provisioning request from the storage
        /// </summary>
        /// <param name="logger"></param>
        /// <returns>Provisioning request</returns>
        Task<ProvisioningRequestModel> GetNextAsync(ILogger logger);
    }
}

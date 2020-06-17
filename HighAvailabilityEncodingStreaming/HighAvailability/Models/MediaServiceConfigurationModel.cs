// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace HighAvailability.Models
{
    /// <summary>
    /// Implements data model to store configuration for Azure Media Services instance
    /// </summary>
    public class MediaServiceConfigurationModel
    {
        /// <summary>
        /// Azure subscription id
        /// </summary>
        public string SubscriptionId { get; set; }

        /// <summary>
        /// Azure resource group name
        /// </summary>
        public string ResourceGroup { get; set; }

        /// <summary>
        /// Azure Media Services instance account name
        /// </summary>
        public string AccountName { get; set; }
    }
}

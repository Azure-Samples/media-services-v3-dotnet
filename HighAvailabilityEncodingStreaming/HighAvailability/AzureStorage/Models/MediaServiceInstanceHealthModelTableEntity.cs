// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace HighAvailability.AzureStorage.Models
{
    using HighAvailability.Models;
    using Microsoft.Azure.Cosmos.Table;
    using System;

    /// <summary>
    /// Implements table storage specific model for MediaServiceInstanceHealthModel class
    /// </summary>
    public class MediaServiceInstanceHealthModelTableEntity : TableEntity
    {
        /// <summary>
        /// Default value for row key
        /// </summary>
        public static readonly string DefaultRowKeyValue = "0";

        /// <summary>
        /// Default constructor
        /// </summary>
        public MediaServiceInstanceHealthModelTableEntity() : this(new MediaServiceInstanceHealthModel())
        {
        }

        /// <summary>
        /// Constructor to create object from MediaServiceInstanceHealthModel object
        /// </summary>
        /// <param name="mediaServiceInstanceHealthModel"></param>
        public MediaServiceInstanceHealthModelTableEntity(MediaServiceInstanceHealthModel mediaServiceInstanceHealthModel)
        {
            this.PartitionKey = mediaServiceInstanceHealthModel.MediaServiceAccountName;
            this.RowKey = DefaultRowKeyValue;
            this.MediaServiceAccountName = mediaServiceInstanceHealthModel.MediaServiceAccountName;
            this.HealthState = mediaServiceInstanceHealthModel.HealthState.ToString();
            this.LastUpdated = mediaServiceInstanceHealthModel.LastUpdated;
            this.IsEnabled = mediaServiceInstanceHealthModel.IsEnabled;
        }

        /// <summary>
        /// Azure Media Services instance account name
        /// </summary>
        public string MediaServiceAccountName { get; set; }

        /// <summary>
        /// Azure Media Services instance health state
        /// </summary>
        public string HealthState { get; set; }

        /// <summary>
        /// Data record update time
        /// </summary>
        public DateTimeOffset LastUpdated { get; set; }

        /// <summary>
        /// Indicator if Azure Media Services instance is enabled and should accept new requests
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Creates MediaServiceInstanceHealthModel object
        /// </summary>
        /// <returns>MediaServiceInstanceHealthModel object</returns>
        public MediaServiceInstanceHealthModel GetMediaServiceInstanceHealthModel()
        {
            return new MediaServiceInstanceHealthModel
            {
                MediaServiceAccountName = this.MediaServiceAccountName,
                HealthState = Enum.Parse<InstanceHealthState>(this.HealthState),
                LastUpdated = this.LastUpdated,
                IsEnabled = this.IsEnabled
            };
        }
    }
}

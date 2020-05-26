namespace HighAvailability.AzureStorage.Models
{
    using HighAvailability.Models;
    using Microsoft.Azure.Cosmos.Table;
    using System;

    public class MediaServiceInstanceHealthModelTableEntity : TableEntity
    {
        public static readonly string DefaultRowKeyValue = "0";

        public MediaServiceInstanceHealthModelTableEntity() : this(new MediaServiceInstanceHealthModel())
        {
        }

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
        /// TBD need to decide if we want to duplicate PartitionKey field in table storage, for now, we are duplicating
        /// </summary>
        public string MediaServiceAccountName { get; set; }
        public string HealthState { get; set; }
        public DateTimeOffset LastUpdated { get; set; }
        public bool IsEnabled { get; set; }

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

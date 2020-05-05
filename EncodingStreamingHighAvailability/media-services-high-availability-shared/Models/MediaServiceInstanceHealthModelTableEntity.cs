namespace media_services_high_availability_shared.Models
{
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
            if (mediaServiceInstanceHealthModel == null)
            {
                throw new ArgumentNullException(nameof(mediaServiceInstanceHealthModel));
            }

            this.PartitionKey = mediaServiceInstanceHealthModel.MediaServiceAccountName;
            this.RowKey = DefaultRowKeyValue;
            this.MediaServiceAccountName = mediaServiceInstanceHealthModel.MediaServiceAccountName;
            this.IsHealthy = mediaServiceInstanceHealthModel.IsHealthy;
            this.LastUpdated = mediaServiceInstanceHealthModel.LastUpdated;
            this.LastSuccessfulJob = mediaServiceInstanceHealthModel.LastSuccessfulJob;
            this.LastFailedJob = mediaServiceInstanceHealthModel.LastFailedJob;
            this.LastSubmittedJob = mediaServiceInstanceHealthModel.LastSubmittedJob;
        }

        /// <summary>
        /// TBD need to decide if we want to duplicate PartitionKey field in table storage, for now, we are duplicating
        /// </summary>
        public string MediaServiceAccountName { get; set; }
        public bool IsHealthy { get; set; }
        public DateTime LastUpdated { get; set; }
        public DateTime LastSuccessfulJob { get; set; }
        public DateTime LastFailedJob { get; set; }
        public DateTime LastSubmittedJob { get; set; }

        public MediaServiceInstanceHealthModel GetMediaServiceInstanceHealthModel()
        {
            return new MediaServiceInstanceHealthModel
            {
                MediaServiceAccountName = this.MediaServiceAccountName,
                IsHealthy = this.IsHealthy,
                LastUpdated = this.LastUpdated,
                LastSuccessfulJob = this.LastSuccessfulJob,
                LastFailedJob = this.LastFailedJob,
                LastSubmittedJob = this.LastSubmittedJob
            };
        }
    }
}

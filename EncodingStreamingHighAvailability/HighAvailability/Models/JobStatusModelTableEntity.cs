namespace HighAvailability.Models
{
    using Microsoft.Azure.Cosmos.Table;
    using System;

    public class JobStatusModelTableEntity : TableEntity
    {
        public JobStatusModelTableEntity() : this(new JobStatusModel())
        {
        }

        public JobStatusModelTableEntity(JobStatusModel jobStatusModel)
        {
            this.PartitionKey = jobStatusModel.JobName;
            this.RowKey = jobStatusModel.Id;
            this.JobStatusId = jobStatusModel.Id;
            this.JobName = jobStatusModel.JobName;
            this.JobOutputAssetName = jobStatusModel.JobOutputAssetName;
            this.State = jobStatusModel.JobState.ToString();
            this.EventTime = jobStatusModel.EventTime;
            this.MediaServiceAccountName = jobStatusModel.MediaServiceAccountName;
            this.TransformName = jobStatusModel.TransformName;
            this.IsSystemError = jobStatusModel.IsSystemError;
        }

        /// <summary>
        /// TBD need to decide if we want to duplicate PartitionKey and RowKey fields in table storage, for now, we are duplicating
        /// We can not use Id field for cosmos db, changed name to JobStatusId
        /// </summary>
        public string JobStatusId { get; set; }
        public string JobName { get; set; }
        public string JobOutputAssetName { get; set; }
        public string State { get; set; }
        public DateTimeOffset EventTime { get; set; }
        public string MediaServiceAccountName { get; set; }
        public string TransformName { get; set; }
        public bool IsSystemError { get; set; }

        public JobStatusModel GetJobStatusModel()
        {
            return new JobStatusModel
            {
                Id = this.JobStatusId,
                JobName = this.JobName,
                JobOutputAssetName = this.JobOutputAssetName,
                JobState = this.State,
                EventTime = this.EventTime,
                MediaServiceAccountName = this.MediaServiceAccountName,
                TransformName = this.TransformName,
                IsSystemError = this.IsSystemError
            };
        }
    }
}

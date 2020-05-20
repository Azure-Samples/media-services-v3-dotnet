namespace HighAvailability.Models
{
    using Microsoft.Azure.Cosmos.Table;
    using System;

    public class JobOutputStatusModelTableEntity : TableEntity
    {
        public JobOutputStatusModelTableEntity() : this(new JobOutputStatusModel())
        {
        }

        public JobOutputStatusModelTableEntity(JobOutputStatusModel jobOutputStatusModel)
        {
            this.PartitionKey = jobOutputStatusModel.JobName;
            this.RowKey = jobOutputStatusModel.Id;
            this.JobOutputStatusId = jobOutputStatusModel.Id;
            this.JobName = jobOutputStatusModel.JobName;
            this.JobOutputAssetName = jobOutputStatusModel.JobOutputAssetName;
            this.JobOutputState = jobOutputStatusModel.JobOutputState.ToString();
            this.EventTime = jobOutputStatusModel.EventTime;
            this.MediaServiceAccountName = jobOutputStatusModel.MediaServiceAccountName;
            this.TransformName = jobOutputStatusModel.TransformName;
            this.IsSystemError = jobOutputStatusModel.IsSystemError;
        }

        /// <summary>
        /// TBD need to decide if we want to duplicate PartitionKey and RowKey fields in table storage, for now, we are duplicating
        /// We can not use Id field for cosmos db, changed name to JobOutputStatusId
        /// </summary>
        public string JobOutputStatusId { get; set; }
        public string JobName { get; set; }
        public string JobOutputAssetName { get; set; }
        public string JobOutputState { get; set; }
        public DateTimeOffset EventTime { get; set; }
        public string MediaServiceAccountName { get; set; }
        public string TransformName { get; set; }
        public bool IsSystemError { get; set; }

        public JobOutputStatusModel GetJobOutputStatusModel()
        {
            return new JobOutputStatusModel
            {
                Id = this.JobOutputStatusId,
                JobName = this.JobName,
                JobOutputAssetName = this.JobOutputAssetName,
                JobOutputState = this.JobOutputState,
                EventTime = this.EventTime,
                MediaServiceAccountName = this.MediaServiceAccountName,
                TransformName = this.TransformName,
                IsSystemError = this.IsSystemError
            };
        }
    }
}

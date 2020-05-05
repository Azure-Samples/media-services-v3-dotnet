namespace media_services_high_availability_shared.Models
{
    using Microsoft.Azure.Management.Media.Models;
    using System;

    public class JobStatusModel
    {
        public JobStatusModel()
        {
            this.Id = string.Empty;
            this.JobName = string.Empty;
            this.JobOutputAssetName = string.Empty;
            this.JobState = JobState.Error;
            this.EventTime = DateTime.MinValue;
            this.MediaServiceAccountName = string.Empty;
        }

        public string Id { get; set; }
        public string JobName { get; set; }
        public string JobOutputAssetName { get; set; }
        public JobState JobState { get; set; }
        public DateTime EventTime { get; set; }
        public string MediaServiceAccountName { get; set; }
    }
}

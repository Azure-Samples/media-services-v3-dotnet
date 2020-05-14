namespace HighAvailability.Models
{
    using Microsoft.Azure.Management.Media.Models;
    using System;

    public class JobStatusModel
    {
        public string Id { get; set; }
        public string JobName { get; set; }
        public string JobOutputAssetName { get; set; }
        public JobState JobState { get; set; }
        public DateTimeOffset EventTime { get; set; }
        public string MediaServiceAccountName { get; set; }
    }
}

namespace HighAvailability.Models
{
    using Microsoft.Azure.Management.Media.Models;
    using System;

    public class JobOutputStatusModel
    {
        public string Id { get; set; }
        public string JobName { get; set; }
        public string JobOutputAssetName { get; set; }
        public JobState JobOutputState { get; set; }
        public DateTimeOffset EventTime { get; set; }
        public string MediaServiceAccountName { get; set; }
        public string TransformName { get; set; }
        public bool IsSystemError { get; set; }
    }
}

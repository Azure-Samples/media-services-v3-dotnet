namespace HighAvailability.Models
{
    using System;

    public class MediaServiceInstanceHealthModel
    {
        public MediaServiceInstanceHealthModel()
        {
            this.MediaServiceAccountName = string.Empty;
            this.IsHealthy = false;
            this.LastUpdated = DateTime.MinValue;
            this.LastSuccessfulJob = DateTime.MinValue;
            this.LastFailedJob = DateTime.MinValue;
            this.LastSubmittedJob = DateTime.MinValue;
        }

        public string MediaServiceAccountName { get; set; }
        public bool IsHealthy { get; set; }
        public DateTime LastUpdated { get; set; }
        public DateTime LastSuccessfulJob { get; set; }
        public DateTime LastFailedJob { get; set; }
        public DateTime LastSubmittedJob { get; set; }
    }
}

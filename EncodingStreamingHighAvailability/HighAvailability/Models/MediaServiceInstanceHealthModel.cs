namespace HighAvailability.Models
{
    using System;

    public class MediaServiceInstanceHealthModel
    {
        public string MediaServiceAccountName { get; set; }
        public bool IsHealthy { get; set; }
        public DateTime LastUpdated { get; set; }
        public DateTime LastSuccessfulJob { get; set; }
        public DateTime LastFailedJob { get; set; }
        public DateTime LastSubmittedJob { get; set; }
    }
}

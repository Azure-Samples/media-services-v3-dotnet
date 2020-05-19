namespace HighAvailability.Models
{
    using System;

    public class MediaServiceInstanceHealthModel
    {
        public string MediaServiceAccountName { get; set; }
        public InstanceHealthState HealthState { get; set; }
        public DateTimeOffset LastUpdated { get; set; }
        public bool IsEnabled { get; set; }
    }

    public enum InstanceHealthState
    {
        Healthy, Degraded, Unhealthy
    }
}

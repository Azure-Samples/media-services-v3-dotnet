namespace HighAvailability.Models
{
    using System;

    /// <summary>
    /// Implements data model to store Azure Media Services instance health data
    /// </summary>
    public class MediaServiceInstanceHealthModel
    {
        /// <summary>
        /// Azure Media Services instance account name
        /// </summary>
        public string MediaServiceAccountName { get; set; }

        /// <summary>
        /// Azure Media Services instance health state
        /// </summary>
        public InstanceHealthState HealthState { get; set; }

        /// <summary>
        /// Data record update time 
        /// </summary>
        public DateTimeOffset LastUpdated { get; set; }

        /// <summary>
        /// Indicator if Azure Media Services instance is enabled and should accept new requests
        /// </summary>
        public bool IsEnabled { get; set; }
    }

    /// <summary>
    /// Enum to indicate Azure Media Services instance health
    /// </summary>
    public enum InstanceHealthState
    {
        /// <summary>
        /// Instance is healthy, accepts new requests
        /// </summary>
        Healthy,

        /// <summary>
        /// Instance is degraded, accepts new requests only if there are no other healthy instance
        /// </summary>
        Degraded,

        /// <summary>
        /// Instance is unhealthy, does not accept new requests
        /// </summary>
        Unhealthy
    }
}

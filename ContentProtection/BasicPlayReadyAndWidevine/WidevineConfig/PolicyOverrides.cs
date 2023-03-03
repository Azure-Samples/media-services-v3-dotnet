// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Newtonsoft.Json;

namespace WidevineConfig
{
    /// <summary>
    /// Widevine PolicyOverrides class.
    /// </summary>
    public class PolicyOverrides
    {
        /// <summary>
        /// Gets or sets a value indicating whether playback of the content is allowed. Default is false.
        /// </summary>
        [JsonProperty("can_play")]
        public bool CanPlay { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the license might be persisted to nonvolatile storage for offline use. Default is false.
        /// </summary>
        [JsonProperty("can_persist")]
        public bool CanPersist { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether renewal of this license is allowed. If true, the duration of the license can be extended by heartbeat. Default is false.
        /// </summary>
        [JsonProperty("can_renew")]
        public bool CanRenew { get; set; }

        /// <summary>
        /// Gets or sets the time window while playback is permitted. A value of 0 indicates that there is no limit to the duration. Default is 0.
        /// </summary>
        [JsonProperty("rental_duration_seconds")]
        public int RentalDurationSeconds { get; set; }

        /// <summary>
        /// Gets or sets the viewing window of time after playback starts within the license duration. A value of 0 indicates that there is no limit to the duration. Default is 0.
        /// </summary>
        [JsonProperty("playback_duration_seconds")]
        public int PlaybackDurationSeconds { get; set; }

        /// <summary>
        /// Gets or sets the time window for this specific license. A value of 0 indicates that there is no limit to the duration. Default is 0.
        /// </summary>
        [JsonProperty("license_duration_seconds")]
        public int LicenseDurationSeconds { get; set; }
    }
}

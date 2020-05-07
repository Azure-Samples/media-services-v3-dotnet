// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Newtonsoft.Json;

namespace BasicWidevine
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

    /// <summary>
    /// Widevine ContentKeySpec class.
    /// </summary>
    public class ContentKeySpec
    {
        /// <summary>
        /// Gets or sets track type.
        /// If content_key_specs is specified in the license request, make sure to specify all track types explicitly.
        /// Failure to do so results in failure to play back past 10 seconds.
        /// </summary>
        [JsonProperty("track_type")]
        public string TrackType { get; set; }

        /// <summary>
        /// Gets or sets client robustness requirements for playback.
        /// Software-based white-box cryptography is required.
        /// Software cryptography and an obfuscated decoder are required.
        /// The key material and cryptography operations must be performed within a hardware-backed trusted execution environment.
        /// The cryptography and decoding of content must be performed within a hardware-backed trusted execution environment.
        /// The cryptography, decoding, and all handling of the media (compressed and uncompressed) must be handled within a hardware-backed trusted execution environment.
        /// </summary>
        [JsonProperty("security_level")]
        public int SecurityLevel { get; set; }

        /// <summary>
        /// Gets or sets the OutputProtection.
        /// </summary>
        [JsonProperty("required_output_protection")]
        public OutputProtection RequiredOutputProtection { get; set; }
    }

    /// <summary>
    /// OutputProtection Widevine class.
    /// </summary>
    public class OutputProtection
    {
        /// <summary>
        /// Gets or sets HDCP protection.
        /// Supported values : HDCP_NONE, HDCP_V1, HDCP_V2
        /// </summary>
        [JsonProperty("hdcp")]
        public string HDCP { get; set; }

        /// <summary>
        /// Gets or sets CGMS.
        /// </summary>
        [JsonProperty("cgms_flags")]
        public string CgmsFlags { get; set; }
    }

    /// <summary>
    /// Widevine template.
    /// </summary>
    public class WidevineTemplate
    {
        /// <summary>
        /// Gets or sets the allowed track types.
        /// SD_ONLY or SD_HD.
        /// Controls which content keys are included in a license.
        /// </summary>
        [JsonProperty("allowed_track_types")]
        public string AllowedTrackTypes { get; set; }

        /// <summary>
        /// Gets or sets a finer-grained control on which content keys to return.
        /// For more information, see the section "Content key specs."
        /// Only one of the allowed_track_types and content_key_specs values can be specified.
        /// </summary>
        [JsonProperty("content_key_specs")]
        public ContentKeySpec[] ContentKeySpecs { get; set; }

        /// <summary>
        /// Gets or sets policy settings for the license.
        /// In the event this asset has a predefined policy, these specified values are used.
        /// </summary>
        [JsonProperty("policy_overrides")]
        public PolicyOverrides PolicyOverrides { get; set; }
    }
}

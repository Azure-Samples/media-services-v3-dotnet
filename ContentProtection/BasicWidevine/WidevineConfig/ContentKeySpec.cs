// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Newtonsoft.Json;

namespace WidevineConfig
{
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
}

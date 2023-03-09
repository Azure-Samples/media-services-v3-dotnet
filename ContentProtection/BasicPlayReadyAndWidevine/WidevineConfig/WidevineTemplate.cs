// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Newtonsoft.Json;

namespace WidevineConfig
{

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

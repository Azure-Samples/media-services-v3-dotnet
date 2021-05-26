// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Newtonsoft.Json;

namespace WidevineConfig
{
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
}

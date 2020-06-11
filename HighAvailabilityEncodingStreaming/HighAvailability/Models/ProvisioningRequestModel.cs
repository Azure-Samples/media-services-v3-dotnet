namespace HighAvailability.Models
{
    /// <summary>
    /// Implements data model to store provisioning request data
    /// </summary>
    public class ProvisioningRequestModel
    {
        /// <summary>
        /// Unique id
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Encoding job output asset name
        /// </summary>
        public string EncodedAssetName { get; set; }

        /// <summary>
        /// Azure Media Services instance account name associated with encoding job
        /// </summary>
        public string EncodedAssetMediaServiceAccountName { get; set; }

        /// <summary>
        /// Streaming locator name to stream encoded asset data
        /// </summary>
        public string StreamingLocatorName { get; set; }
    }
}

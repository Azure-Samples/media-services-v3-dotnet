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
        /// Processed asset name
        /// </summary>
        public string ProcessedAssetName { get; set; }

        /// <summary>
        /// Azure Media Services instance account name associated with processing job
        /// </summary>
        public string ProcessedAssetMediaServiceAccountName { get; set; }

        /// <summary>
        /// Streaming locator name to stream processed asset data
        /// </summary>
        public string StreamingLocatorName { get; set; }
    }
}

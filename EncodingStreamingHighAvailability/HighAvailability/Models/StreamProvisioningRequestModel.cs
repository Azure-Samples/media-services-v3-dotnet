namespace HighAvailability.Models
{
    public class StreamProvisioningRequestModel
    {
        public StreamProvisioningRequestModel()
        {
            this.Id = string.Empty;
            this.EncodedAssetMediaServiceAccountName = string.Empty;
            this.EncodedAssetName = string.Empty;
            this.StreamingLocatorName = string.Empty;
        }

        public string Id { get; set; }
        public string EncodedAssetName { get; set; }
        public string EncodedAssetMediaServiceAccountName { get; set; }
        public string StreamingLocatorName { get; set; }
    }
}

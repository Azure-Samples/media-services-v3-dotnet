namespace HighAvailability.Models
{
    public class ProvisioningRequestModel
    {
        public string Id { get; set; }
        public string EncodedAssetName { get; set; }
        public string EncodedAssetMediaServiceAccountName { get; set; }
        public string StreamingLocatorName { get; set; }
    }
}

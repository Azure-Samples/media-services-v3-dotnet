namespace HighAvailability.Models
{
    public class ProvisioningCompletedEventModel
    {
        public string Id { get; set; }
        public string AssetName { get; set; }
        public string MediaServiceAccountName { get; set; }
        public string StreamingLocatorName { get; set; }
#pragma warning disable CA1056 // Uri properties should not be strings
        public string PrimaryUrl { get; set; }
#pragma warning restore CA1056 // Uri properties should not be strings
    }
}

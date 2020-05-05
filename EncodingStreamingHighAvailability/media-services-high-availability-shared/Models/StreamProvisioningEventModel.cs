namespace media_services_high_availability_shared.Models
{
    public class StreamProvisioningEventModel
    {
        public StreamProvisioningEventModel()
        {
            this.Id = string.Empty;
            this.AssetName = string.Empty;
            this.MediaServiceAccountName = string.Empty;
            this.StreamingLocatorName = string.Empty;
            this.PrimaryUrl = string.Empty;
        }

        public string Id { get; set; }
        public string AssetName { get; set; }
        public string MediaServiceAccountName { get; set; }
        public string StreamingLocatorName { get; set; }
#pragma warning disable CA1056 // Uri properties should not be strings
        public string PrimaryUrl { get; set; }
#pragma warning restore CA1056 // Uri properties should not be strings
    }
}

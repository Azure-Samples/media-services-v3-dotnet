namespace HighAvailability.Models
{
    public class JobVerificationRequestModel
    {
        public string Id { get; set; }
        public string JobId { get; set; }
        public string MediaServiceAccountName { get; set; }
        public JobRequestModel OriginalJobRequestModel { get; set; }
        public string JobOutputAssetName { get; set; }
        public string JobName { get; set; }
        public int RetryCount { get; set; }
    }
}

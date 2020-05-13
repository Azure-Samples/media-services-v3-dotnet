namespace HighAvailability.Models
{
    public class JobVerificationRequestModel
    {
        public string Id { get; set; }
        public string JobId { get; set; }
        public string MediaServiceAccountName { get; set; }
        public JobRequestModel JobRequest { get; set; }
    }
}

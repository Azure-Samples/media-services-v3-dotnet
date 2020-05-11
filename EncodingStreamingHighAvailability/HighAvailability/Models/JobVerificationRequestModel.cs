namespace HighAvailability.Models
{
    public class JobVerificationRequestModel
    {
        public JobVerificationRequestModel()
        {
            this.Id = string.Empty;
            this.JobId = string.Empty;
            this.MediaServiceAccountName = string.Empty;
            this.JobRequest = new JobRequestModel();
        }

        public string Id { get; set; }
        public string JobId { get; set; }
        public string MediaServiceAccountName { get; set; }
        public JobRequestModel JobRequest { get; set; }
    }
}

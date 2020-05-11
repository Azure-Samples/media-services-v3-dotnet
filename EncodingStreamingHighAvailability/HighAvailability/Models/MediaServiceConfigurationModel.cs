namespace HighAvailability.Models
{
    public class MediaServiceConfigurationModel
    {
        public MediaServiceConfigurationModel()
        {
            this.SubscriptionId = string.Empty;
            this.ResourceGroup = string.Empty;
            this.AccountName = string.Empty;
        }
        public string SubscriptionId { get; set; }

        public string ResourceGroup { get; set; }

        public string AccountName { get; set; }
    }
}

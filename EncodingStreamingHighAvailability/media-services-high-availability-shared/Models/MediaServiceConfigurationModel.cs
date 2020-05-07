#pragma warning disable CA1707 // Identifiers should not contain underscores
namespace media_services_high_availability_shared.Models
#pragma warning restore CA1707 // Identifiers should not contain underscores
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

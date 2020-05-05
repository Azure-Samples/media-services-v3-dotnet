#pragma warning disable CA1707 // Identifiers should not contain underscores
namespace media_services_high_availability_shared.Models
#pragma warning restore CA1707 // Identifiers should not contain underscores
{
    using System;

    public class MediaServiceConfigurationModel
    {
        public MediaServiceConfigurationModel()
        {
            this.SubscriptionId = string.Empty;
            this.ResourceGroup = string.Empty;
            this.AccountName = string.Empty;
            this.AadClientId = string.Empty;
            this.AadTenantId = string.Empty;
            this.AadSecret = string.Empty;
            this.ArmAadAudience = new Uri("http://contoso.com");
            this.AadEndpoint = new Uri("http://contoso.com");
            this.ArmEndpoint = new Uri("http://contoso.com");
            this.Region = string.Empty;

        }
        public string SubscriptionId { get; set; }

        public string ResourceGroup { get; set; }

        public string AccountName { get; set; }

        public string AadTenantId { get; set; }

        public string AadClientId { get; set; }

        public string AadSecret { get; set; }

        public Uri ArmAadAudience { get; set; }

        public Uri AadEndpoint { get; set; }

        public Uri ArmEndpoint { get; set; }

        public string Region { get; set; }
    }
}

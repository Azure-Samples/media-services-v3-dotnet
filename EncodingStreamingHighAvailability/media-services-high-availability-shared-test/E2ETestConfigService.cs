namespace media_services_high_availability_shared_test
{
    using media_services_high_availability_shared.Models;
    using media_services_high_availability_shared.Services;
    using Microsoft.Azure.KeyVault;
    using Microsoft.Azure.Services.AppAuthentication;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public class E2ETestConfigService : IConfigService
    {
        private readonly TestContext testContext;
        private readonly string keyVaultName;

        public E2ETestConfigService(string keyVaultName, TestContext testContext)
        {
            this.keyVaultName = keyVaultName ?? throw new ArgumentNullException(nameof(keyVaultName));
            this.testContext = testContext ?? throw new ArgumentNullException(nameof(testContext));
            this.MediaServiceInstanceHealthTableName = "MediaServiceInstanceHealth";
            this.JobStatusTableName = "JobStatus";
            this.StreamProvisioningRequestQueueName = "stream-provisioning-requests";
            this.JobVerificationRequestQueueName = "job-verification-requests";
            this.JobRequestQueueName = "job-requests";
            this.StreamProvisioningEventQueueName = "stream-provisioning-events";
            this.MediaServiceInstanceConfiguration = new Dictionary<string, MediaServiceConfigurationModel>();
            this.StorageAccountConnectionString = string.Empty;
            this.TableStorageAccountConnectionString = string.Empty;
            this.FrontDoorHostName = "contoso.com";
        }

        public string MediaServiceInstanceHealthTableName { get; private set; }

        public string JobStatusTableName { get; private set; }

        public string StorageAccountConnectionString { get; private set; }

        public string TableStorageAccountConnectionString { get; private set; }

        public string StreamProvisioningRequestQueueName { get; private set; }

        public string JobVerificationRequestQueueName { get; private set; }

        public string JobRequestQueueName { get; private set; }

        public string StreamProvisioningEventQueueName { get; private set; }

        public string FrontDoorHostName { get; private set; }

        public IDictionary<string, MediaServiceConfigurationModel> MediaServiceInstanceConfiguration { get; private set; }

        public async Task LoadConfigurationAsync()
        {
            var azureServiceTokenProvider = new AzureServiceTokenProvider();
            using (var keyVaultClient = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(azureServiceTokenProvider.KeyVaultTokenCallback)))
            {
                var secret = await keyVaultClient.GetSecretAsync($"https://{this.keyVaultName}.vault.azure.net/secrets/StorageAccountConnectionString").ConfigureAwait(false);
                this.StorageAccountConnectionString = secret.Value;

                secret = await keyVaultClient.GetSecretAsync($"https://{this.keyVaultName}.vault.azure.net/secrets/TableStorageAccountConnectionString").ConfigureAwait(false);
                this.TableStorageAccountConnectionString = secret.Value;
            }

            // Copy this from azure function configuration AMSConfiguration key. 
            // Use advanced edit in configuration screen to get value with encoded quotes
            var amsConfigurationString = "[{\"SubscriptionId\":\"465d1912-ea98-4b36-94d2-fabbf55fe648\",\"ResourceGroup\":\"ha-test\",\"AccountName\":\"sipetrikha2amseastus\"},{\"SubscriptionId\":\"465d1912-ea98-4b36-94d2-fabbf55fe648\",\"ResourceGroup\":\"ha-test\",\"AccountName\":\"sipetrikha2amswestus\"},{\"SubscriptionId\":\"465d1912-ea98-4b36-94d2-fabbf55fe648\",\"ResourceGroup\":\"ha-test\",\"AccountName\":\"sipetrikha2amswesteurope\"}]";
            var amsConfigurationList = JsonConvert.DeserializeObject<List<MediaServiceConfigurationModel>>(amsConfigurationString);
            this.MediaServiceInstanceConfiguration = amsConfigurationList.ToDictionary(i => i.AccountName);
        }
    }
}

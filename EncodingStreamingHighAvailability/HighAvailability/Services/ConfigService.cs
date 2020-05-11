namespace HighAvailability.Services
{
    using HighAvailability.Models;
    using Microsoft.Azure.KeyVault;
    using Microsoft.Azure.Services.AppAuthentication;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public class ConfigService : IConfigService
    {
        private readonly string keyVaultName;
        private readonly string AMSConfigurationKeyName = "AMSConfiguration";
        private readonly string FrontDoorHostNameKeyName = "FrontDoorHostName";

        public ConfigService(string keyVaultName)
        {
            this.keyVaultName = keyVaultName ?? throw new ArgumentNullException(nameof(keyVaultName));
            this.MediaServiceInstanceHealthTableName = "MediaServiceInstanceHealth";
            this.JobStatusTableName = "JobStatus";
            this.StreamProvisioningRequestQueueName = "stream-provisioning-requests";
            this.JobVerificationRequestQueueName = "job-verification-requests";
            this.JobRequestQueueName = "job-requests";
            this.StreamProvisioningEventQueueName = "stream-provisioning-events";
            this.MediaServiceInstanceConfiguration = new Dictionary<string, MediaServiceConfigurationModel>();
            this.StorageAccountConnectionString = string.Empty;
            this.TableStorageAccountConnectionString = string.Empty;
            this.FrontDoorHostName = string.Empty;
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

            var amsConfigurationString = Environment.GetEnvironmentVariable(this.AMSConfigurationKeyName);

            if (amsConfigurationString == null)
            {
                throw new Exception($"Function confo does not have {this.AMSConfigurationKeyName} value");
            }

            var amsConfigurationList = JsonConvert.DeserializeObject<List<MediaServiceConfigurationModel>>(amsConfigurationString);
            this.MediaServiceInstanceConfiguration = amsConfigurationList.ToDictionary(i => i.AccountName);

            this.FrontDoorHostName = Environment.GetEnvironmentVariable(this.FrontDoorHostNameKeyName);

            if (this.FrontDoorHostName == null)
            {
                throw new Exception($"Function confo does not have {this.FrontDoorHostNameKeyName} value");
            }
        }
    }
}

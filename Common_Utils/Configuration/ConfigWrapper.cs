// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Configuration;
using System;

namespace Common_Utils
{
    /// <summary>
    /// This class reads values from local configuration file resources/conf/appsettings.json.
    /// Please change the configuration using your account information. For more information, see
    /// https://learn.microsoft.com/azure/media-services/latest/access-api-cli-how-to. For security
    /// reasons, do not check in the configuration file to source control.
    /// </summary>
    public class ConfigWrapper
    {
        private readonly IConfiguration _config;

        public ConfigWrapper(IConfiguration config)
        {
            _config = config;
        }

        public string SubscriptionId
        {
            get { return _config["AZURE_SUBSCRIPTION_ID"]; }
        }

        public string ResourceGroup
        {
            get { return _config["AZURE_RESOURCE_GROUP"]; }
        }

        public string AccountName
        {
            get { return _config["AZURE_MEDIA_SERVICES_ACCOUNT_NAME"]; }
        }

        public string AadTenantId
        {
            get { return _config["AZURE_TENANT_ID"]; }
        }

        public string AadClientId
        {
            get { return _config["AZURE_CLIENT_ID"]; }
        }

        public string AadSecret
        {
            get { return _config["AZURE_CLIENT_SECRET"]; }
        }

        public Uri ArmAadAudience
        {
            get { return new Uri(_config["AZURE_ARM_TOKEN_AUDIENCE"]); }
        }

        public Uri AadEndpoint
        {
            get { return new Uri(_config["AZURE_AAD_ENDPOINT"]); }
        }

        public Uri ArmEndpoint
        {
            get { return new Uri(_config["AZURE_ARM_ENDPOINT"]); }
        }

        public string EventHubConnectionString
        {
            get { return _config["EVENTHUBCONNECTIONSTRING"]; }
        }

        public string EventHubName
        {
            get { return _config["EVENTHUBNAME"]; }
        }

        public string EventHubConsumerGroup
        {
            get { return _config["EVENTCONSUMERGROUP"]; }
        }

        public string StorageContainerName
        {
            get { return _config["STORAGECONTAINERNAME"]; }
        }

        public string StorageAccountName
        {
            get { return _config["STORAGEACCOUNTNAME"]; }
        }

        public string StorageAccountKey
        {
            get { return _config["STORAGEACCOUNTKEY"]; }
        }

        public string StorageConnectionString
        {
            get { return _config["STORAGECONNECTIONSTRING"]; }
        }

        public string SymmetricKey
        {
            get { return _config["SYMMETRICKEY"]; }
        }

        public string AskHex
        {
            get { return _config["AskHex"]; }
        }

        public string FairPlayPfxPath
        {
            get { return _config["FairPlayPfxPath"]; }
        }

        public string FairPlayPfxPassword
        {
            get { return _config["FairPlayPfxPassword"]; }
        }
    }
}






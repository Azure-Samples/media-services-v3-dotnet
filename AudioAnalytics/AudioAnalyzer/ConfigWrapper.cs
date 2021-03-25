// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using Microsoft.Extensions.Configuration;

namespace AudioAnalyzer
{
    /// <summary>
    /// This class reads values from local configuration file appsettings.json
    /// Please change the configuration using your account information. For more information, see
    /// https://docs.microsoft.com/azure/media-services/latest/access-api-cli-how-to. For security
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
            get { return _config["SubscriptionId"]; }
        }

        public string ResourceGroup
        {
            get { return _config["ResourceGroup"]; }
        }

        public string AccountName
        {
            get { return _config["AccountName"]; }
        }

        public string AadTenantId
        {
            get { return _config["AadTenantId"]; }
        }

        public string AadClientId
        {
            get { return _config["AadClientId"]; }
        }

        public string AadSecret
        {
            get { return _config["AadSecret"]; }
        }

        public Uri ArmAadAudience
        {
            get { return new Uri(_config["ArmAadAudience"]); }
        }

        public Uri AadEndpoint
        {
            get { return new Uri(_config["AadEndpoint"]); }
        }

        public Uri ArmEndpoint
        {
            get { return new Uri(_config["ArmEndpoint"]); }
        }

        public string EventHubConnectionString
        {
            get { return _config["EventHubConnectionString"]; }
        }

        public string EventHubName
        {
            get { return _config["EventHubName"]; }
        }

        public string StorageContainerName
        {
            get { return _config["StorageContainerName"]; }
        }

        public string StorageAccountName
        {
            get { return _config["StorageAccountName"]; }
        }

        public string StorageAccountKey
        {
            get { return _config["StorageAccountKey"]; }
        }
    }
}

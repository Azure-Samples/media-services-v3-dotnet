// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Newtonsoft.Json.Linq;
using System;
using System.IO;

namespace MigrationSample
{
    /// <summary>
    /// This class reads values from local configuration file appsettings.json
    /// Please change the configuration using your account information. For more information, see
    /// https://docs.microsoft.com/azure/media-services/latest/access-api-cli-how-to. For security
    /// reasons, do not check in the configuration file to source control.
    /// </summary>
    public class ConfigWrapper
    {
        private JObject _config;

        public ConfigWrapper(string pathToJsonConfigFile)
        {
            string configText = File.ReadAllText(pathToJsonConfigFile);
            _config = JObject.Parse(configText);
        }

        public string SubscriptionId
        {
            get { return _config["SubscriptionId"].Value<string>(); }
        }

        public string ResourceGroup
        {
            get { return _config["ResourceGroup"].Value<string>(); }
        }

        public string AccountName
        {
            get { return _config["AccountName"].Value<string>(); }
        }

        public string AadTenantId
        {
            get { return _config["AadTenantId"].Value<string>(); }
        }

        public string AadClientId
        {
            get { return _config["AadClientId"].Value<string>(); }
        }

        public string AadSecret
        {
            get { return _config["AadSecret"].Value<string>(); }
        }

        public Uri ArmAadAudience
        {
            get { return new Uri(_config["ArmAadAudience"].Value<string>()); }
        }

        public Uri AadEndpoint
        {
            get { return new Uri(_config["AadEndpoint"].Value<string>()); }
        }

        public Uri ArmEndpoint
        {
            get { return new Uri(_config["ArmEndpoint"].Value<string>()); }
        }

        public string Region
        {
            get { return _config["Region"].Value<string>(); }
        }

        public string EventHubConnectionString
        {
            get { return _config["EventHubConnectionString"].Value<string>(); }
        }

        public string EventHubName
        {
            get { return _config["EventHubName"].Value<string>(); }
        }

        public string StorageContainerName
        {
            get { return _config["StorageContainerName"].Value<string>(); }
        }

        public string StorageAccountName
        {
            get { return _config["StorageAccountName"].Value<string>(); }
        }

        public string StorageAccountKey
        {
            get { return _config["StorageAccountKey"].Value<string>(); }
        }

        public Uri AmsRestApiEndpoint
        {
            get { return new Uri(_config["AmsRestApiEndpoint"].Value<string>()); }
        }

        public string AadTenantDomain
        {
            get { return _config["AadTenantDomain"].Value<string>(); }
        }

        public string FilePathToUpload
        {
            get { return _config["FilePathToUpload"].Value<string>(); }
        }

    }
}

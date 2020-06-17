// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace HighAvailability.Factories
{
    using HighAvailability.Interfaces;
    using Microsoft.Azure.Management.Media;
    using Microsoft.Azure.Services.AppAuthentication;
    using Microsoft.Extensions.Logging;
    using Microsoft.Rest;
    using System;
    using System.Net.Http;

    /// <summary>
    /// Factory class for creating IAzureMediaServicesClient instances
    /// </summary>
    public class MediaServiceInstanceFactory : IMediaServiceInstanceFactory
    {
        /// <summary>
        /// Configuration container to store data about media services instances
        /// </summary>
        private readonly IConfigService configService;

        /// <summary>
        /// Storage service to persist status of all calls to Media Services APIs
        /// </summary>
        private readonly IMediaServiceCallHistoryStorageService mediaServiceCallHistoryStorageService;

        /// <summary>
        /// Single Azure Media Client instance that is used for all calls
        /// </summary>
        private IAzureMediaServicesClient azureMediaServicesClient;

        /// <summary>
        /// Object used to sync access to azureMediaServicesClient
        /// </summary>
        private object azureMediaServicesClientLockObject;

        /// <summary>
        /// flag to indicate that client reset is requested
        /// </summary>
        private bool resetRequested;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="mediaServiceCallHistoryStorageService">Service to store Media Services call history</param>
        /// <param name="configService">configuration container that stores data about Azure Media Service instances</param>
        public MediaServiceInstanceFactory(IMediaServiceCallHistoryStorageService mediaServiceCallHistoryStorageService, IConfigService configService)
        {
            this.mediaServiceCallHistoryStorageService = mediaServiceCallHistoryStorageService ?? throw new ArgumentNullException(nameof(mediaServiceCallHistoryStorageService));
            this.configService = configService ?? throw new ArgumentNullException(nameof(configService));
            this.azureMediaServicesClientLockObject = new object();
            this.resetRequested = false;
        }

        /// <summary>
        /// Returns instance of IAzureMediaServicesClient to connect to specific Azure Media Service instance.
        /// </summary>
        /// <param name="accountName">Azure Media Service account name</param>
        /// <param name="logger">Logger to log data</param>
        /// <returns>Created client</returns>
        public IAzureMediaServicesClient GetMediaServiceInstance(string accountName, ILogger logger)
        {
            if (!this.configService.MediaServiceInstanceConfiguration.ContainsKey(accountName))
            {
                throw new ArgumentException($"Invalid accountName {accountName}");
            }

            lock (this.azureMediaServicesClientLockObject)
            {
                if (this.azureMediaServicesClient == null || this.resetRequested)
                {
                    var azureServiceTokenProvider = new AzureServiceTokenProvider();
                    var accessToken = azureServiceTokenProvider.GetAccessTokenAsync("https://management.azure.com").GetAwaiter().GetResult();
                    ServiceClientCredentials credentials = new TokenCredentials(accessToken);

                    // Establish a connection to Media Services.
                    this.azureMediaServicesClient = new AzureMediaServicesClient(credentials,
                        new DelegatingHandler[] { new CallHistoryHandler(this.mediaServiceCallHistoryStorageService, this, logger) })
                    {
                        SubscriptionId = this.configService.MediaServiceInstanceConfiguration[accountName].SubscriptionId
                    };

                    this.resetRequested = false;
                }
            }

            return this.azureMediaServicesClient;
        }

        /// <summary>
        /// Resets Media Service client. This should be used when error happens and new client connection is required.
        /// </summary>
        /// <returns>Async operation result</returns>
        public void ResetMediaServiceInstance()
        {
            lock (this.azureMediaServicesClientLockObject)
            {
                // this will force to recreate client on next call
                this.resetRequested = true;
            }
        }
    }
}

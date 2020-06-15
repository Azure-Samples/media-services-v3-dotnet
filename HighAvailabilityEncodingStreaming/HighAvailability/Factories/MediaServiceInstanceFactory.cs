namespace HighAvailability.Factories
{
    using HighAvailability.Interfaces;
    using Microsoft.Azure.Management.Media;
    using Microsoft.Azure.Services.AppAuthentication;
    using Microsoft.Extensions.Logging;
    using Microsoft.Rest;
    using System;
    using System.Net.Http;
    using System.Threading.Tasks;

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
        /// Constructor
        /// </summary>
        /// <param name="mediaServiceCallHistoryStorageService">Service to store Media Services call history</param>
        /// <param name="configService">configuration container that stores data about Azure Media Service instances</param>
        public MediaServiceInstanceFactory(IMediaServiceCallHistoryStorageService mediaServiceCallHistoryStorageService, IConfigService configService)
        {
            this.mediaServiceCallHistoryStorageService = mediaServiceCallHistoryStorageService ?? throw new ArgumentNullException(nameof(mediaServiceCallHistoryStorageService));
            this.configService = configService ?? throw new ArgumentNullException(nameof(configService));
        }

        /// <summary>
        /// Returns instance of IAzureMediaServicesClient to connect to specific Azure Media Service instance.
        /// </summary>
        /// <param name="accountName">Azure Media Service account name</param>
        /// <param name="logger">Logger to log data</param>
        /// <returns>Created client</returns>
        public async Task<IAzureMediaServicesClient> GetMediaServiceInstanceAsync(string accountName, ILogger logger)
        {
            if (!this.configService.MediaServiceInstanceConfiguration.ContainsKey(accountName))
            {
                throw new ArgumentException($"Invalid accountName {accountName}");
            }

            if (this.azureMediaServicesClient == null)
            {
                var azureServiceTokenProvider = new AzureServiceTokenProvider();
                var accessToken = await azureServiceTokenProvider.GetAccessTokenAsync("https://management.azure.com").ConfigureAwait(false);
                ServiceClientCredentials credentials = new TokenCredentials(accessToken);

                // Establish a connection to Media Services.
                this.azureMediaServicesClient = new AzureMediaServicesClient(credentials,
                    new DelegatingHandler[] { new CallHistoryHandler(this.mediaServiceCallHistoryStorageService, logger) })
                {
                    SubscriptionId = this.configService.MediaServiceInstanceConfiguration[accountName].SubscriptionId
                };
            }

            return this.azureMediaServicesClient;
        }
    }
}

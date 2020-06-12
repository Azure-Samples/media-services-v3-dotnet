namespace HighAvailability.Factories
{
    using HighAvailability.Helpers;
    using HighAvailability.Interfaces;
    using Microsoft.Azure.Management.Media;
    using System;
    using System.Collections;
    using System.Collections.Generic;
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
        /// Single Azure Media Client instance that is used for all calls
        /// </summary>
        private IAzureMediaServicesClient azureMediaServicesClient;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="configService">configuration container that stores data about Azure Media Service instances</param>
        public MediaServiceInstanceFactory(IConfigService configService)
        {
            this.configService = configService ?? throw new ArgumentNullException(nameof(configService));
        }

        /// <summary>
        /// Returns instance of IAzureMediaServicesClient to connect to specific Azure Media Service instance.
        /// </summary>
        /// <param name="accountName">Azure Media Service account name</param>
        /// <returns></returns>
        public async Task<IAzureMediaServicesClient> GetMediaServiceInstanceAsync(string accountName)
        {
            if (!this.configService.MediaServiceInstanceConfiguration.ContainsKey(accountName))
            {
                throw new ArgumentException($"Invalid accountName {accountName}");
            }

            if (this.azureMediaServicesClient == null)
            {
                this.azureMediaServicesClient = await MediaServicesHelper.CreateMediaServicesClientAsync(this.configService.MediaServiceInstanceConfiguration[accountName]).ConfigureAwait(false);
            }

            return this.azureMediaServicesClient;
        }
    }
}

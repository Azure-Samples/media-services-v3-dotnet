namespace HighAvailability.Services
{
    using HighAvailability.Helpers;
    using Microsoft.Azure.Management.Media;
    using System;
    using System.Threading.Tasks;

    public class MediaServiceInstanceFactory : IMediaServiceInstanceFactory
    {
        private readonly IConfigService configService;

        public MediaServiceInstanceFactory(IConfigService configService)
        {
            this.configService = configService ?? throw new ArgumentNullException(nameof(configService));
        }

        public async Task<IAzureMediaServicesClient> GetMediaServiceInstanceAsync(string accountName)
        {
            if (!this.configService.MediaServiceInstanceConfiguration.ContainsKey(accountName))
            {
                throw new ArgumentException($"Invalid accountName {accountName}");
            }

            return await MediaServicesHelper.CreateMediaServicesClientAsync(this.configService.MediaServiceInstanceConfiguration[accountName]).ConfigureAwait(false);
        }
    }
}

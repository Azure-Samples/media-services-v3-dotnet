namespace HighAvailability.Tests
{
    using HighAvailability.Helpers;
    using HighAvailability.Services;
    using Microsoft.Azure.Management.Media;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System.Threading.Tasks;

    [TestClass]
    public class Cleanup
    {
        private static IConfigService configService;

        [ClassInitialize]
        public static async Task Initialize(TestContext _)
        {
            configService = new E2ETestConfigService("sipetrik-keyvault");
            await configService.LoadConfigurationAsync().ConfigureAwait(false);
        }

        [TestMethod]
        public async Task CleanupAssets()
        {
            var configuration = configService.MediaServiceInstanceConfiguration;
            foreach (var config in configuration.Values)
            {
                var client = await MediaServicesHelper.CreateMediaServicesClientAsync(config).ConfigureAwait(false);
                var assets = await client.Assets.ListAsync(config.ResourceGroup, config.AccountName).ConfigureAwait(false);
                foreach (var asset in assets)
                {
                    await client.Assets.DeleteAsync(config.ResourceGroup, config.AccountName, asset.Name).ConfigureAwait(false);
                }

                var transforms = await client.Transforms.ListAsync(config.ResourceGroup, config.AccountName).ConfigureAwait(false);
                foreach (var transform in transforms)
                {
                    await client.Transforms.DeleteAsync(config.ResourceGroup, config.AccountName, transform.Name).ConfigureAwait(false);
                }
            }
        }
    }
}
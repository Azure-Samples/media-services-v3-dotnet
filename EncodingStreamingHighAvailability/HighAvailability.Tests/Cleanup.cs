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
        private static IConfigService? configService;

        [ClassInitialize]
        public static async Task Initialize(TestContext testContext)
        {
            if (testContext is null)
            {
                throw new System.ArgumentNullException(nameof(testContext));
            }

            configService = new E2ETestConfigService("sipetrikha2-keyvault");
            await configService.LoadConfigurationAsync().ConfigureAwait(false);
        }

        [TestMethod]
        public async Task CleanupAssets()
        {
            if (configService == null)
            {
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                throw new System.Exception("Config services is not initialized");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
            }

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
                    var jobs = await client.Jobs.ListAsync(config.ResourceGroup, config.AccountName, transform.Name).ConfigureAwait(false);
                    foreach (var job in jobs)
                    {
                        await client.Jobs.DeleteAsync(config.ResourceGroup, config.AccountName, transform.Name, job.Name).ConfigureAwait(false);
                    }
                }
            }
        }
    }
}
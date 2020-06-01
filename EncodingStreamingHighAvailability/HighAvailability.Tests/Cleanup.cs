namespace HighAvailability.Tests
{
    using HighAvailability.Factories;
    using HighAvailability.Interfaces;
    using Microsoft.Azure.Management.Media;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System.Threading.Tasks;

    /// <summary>
    /// This is a class to implement Azure Media Service clien up
    /// </summary>
    [TestClass]
    public class Cleanup
    {
        /// <summary>
        /// Configuration container
        /// </summary>
        private static IConfigService configService;

        /// <summary>
        /// Initialize environment
        /// </summary>
        /// <param name="_">Not used</param>
        /// <returns>Task of async operation</returns>
        [ClassInitialize]
        public static async Task Initialize(TestContext _)
        {
            // TBD remove keyvault value
            configService = new E2ETestConfigService("sipetrik-keyvault");
            await configService.LoadConfigurationAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Cleans up all assets and transforms with associated jobs
        /// </summary>
        /// <returns>Task of async operation</returns>
        [TestMethod]
        public async Task CleanupAssets()
        {
            var configuration = configService.MediaServiceInstanceConfiguration;
            var mediaServiceInstanceFactory = new MediaServiceInstanceFactory(configService);
            foreach (var config in configuration.Values)
            {
                var client = await mediaServiceInstanceFactory.GetMediaServiceInstanceAsync(config.AccountName).ConfigureAwait(false);
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
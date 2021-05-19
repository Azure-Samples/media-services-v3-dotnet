// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Azure.Management.Media;
using Microsoft.Identity.Client;
using Microsoft.Rest;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Common_Utils
{
    public class Authentication
    {
        public static readonly string TokenType = "Bearer";

        /// <summary>
        /// Creates the AzureMediaServicesClient object based on the credentials
        /// supplied in local configuration file.
        /// </summary>
        /// <param name="config">The param is of type ConfigWrapper, which reads values from local configuration file.</param>
        /// <returns>A task.</returns>
        // <CreateMediaServicesClientAsync>
        public static async Task<IAzureMediaServicesClient> CreateMediaServicesClientAsync(ConfigWrapper config, bool interactive = false)
        {
            ServiceClientCredentials credentials;
            if (interactive)
                credentials = await GetCredentialsInteractiveAuthAsync(config);
            else
                credentials = await GetCredentialsAsync(config);

            return new AzureMediaServicesClient(config.ArmEndpoint, credentials)
            {
                SubscriptionId = config.SubscriptionId,
            };
        }
        // </CreateMediaServicesClientAsync>

        /// <summary>
        /// Create the ServiceClientCredentials object based on the credentials
        /// supplied in local configuration file.
        /// </summary>
        /// <param name="config">The param is of type ConfigWrapper. This class reads values from local configuration file.</param>
        /// <returns></returns>
        // <GetCredentialsAsync>
        private static async Task<ServiceClientCredentials> GetCredentialsAsync(ConfigWrapper config)
        {
            // Use ConfidentialClientApplicationBuilder.AcquireTokenForClient to get a token using a service principal with symmetric key

            var scopes = new[] { config.ArmAadAudience + "/.default" };

            var app = ConfidentialClientApplicationBuilder.Create(config.AadClientId)
                .WithClientSecret(config.AadSecret)
                .WithAuthority(AzureCloudInstance.AzurePublic, config.AadTenantId)
                .Build();

            var authResult = await app.AcquireTokenForClient(scopes)
                                                     .ExecuteAsync()
                                                     .ConfigureAwait(false);

            return new TokenCredentials(authResult.AccessToken, TokenType);
        }
        // </GetCredentialsAsync>

        /// <summary>
        /// Create the ServiceClientCredentials object based on interactive authentication done in the browser
        /// </summary>
        /// <param name="config">The param is of type ConfigWrapper. This class reads values from local configuration file.</param>
        /// <returns></returns>
        private static async Task<ServiceClientCredentials> GetCredentialsInteractiveAuthAsync(ConfigWrapper config)
        {
            var scopes = new[] { config.ArmAadAudience + "/user_impersonation" };

            // client application of Az Cli
            string ClientApplicationId = "04b07795-8ddb-461a-bbee-02f9e1bf7b46";

            AuthenticationResult result = null;

            IPublicClientApplication app = PublicClientApplicationBuilder.Create(ClientApplicationId)
                .WithAuthority(AzureCloudInstance.AzurePublic, config.AadTenantId)
                .WithRedirectUri("http://localhost")
                .Build();

            var accounts = await app.GetAccountsAsync();

            try
            {
                result = await app.AcquireTokenSilent(scopes, accounts.FirstOrDefault()).ExecuteAsync();
            }
            catch (MsalUiRequiredException ex)
            {
                try
                {
                    result = await app.AcquireTokenInteractive(scopes).ExecuteAsync();
                }
                catch (MsalException maslException)
                {
                    Console.Error.WriteLine($"ERROR: MSAL interactive authentication exception with code '{maslException.ErrorCode}' and message '{maslException.Message}'.");
                }
            }
            catch (MsalException maslException)
            {
                Console.Error.WriteLine($"ERROR: MSAL silent authentication exception with code '{maslException.ErrorCode}' and message '{maslException.Message}'.");
            }

            return new TokenCredentials(result.AccessToken, TokenType);
        }
    }
}

namespace HighAvailability.Helpers
{
    using HighAvailability.Models;
    using Microsoft.Azure.EventGrid.Models;
    using Microsoft.Azure.Management.Media;
    using Microsoft.Azure.Management.Media.Models;
    using Microsoft.Azure.Services.AppAuthentication;
    using Microsoft.IdentityModel.Tokens;
    using Microsoft.Rest;
    using System;
    using System.Collections.Generic;
    using System.IdentityModel.Tokens.Jwt;
    using System.Security.Claims;
    using System.Threading.Tasks;

    public static class MediaServicesHelper
    {
        /// <summary>
        /// Creates the AzureMediaServicesClient object based on the credentials
        /// supplied in local configuration file.
        /// </summary>
        /// <param name="config">The parm is of type ConfigWrapper. This class reads values from local configuration file.</param>
        /// <returns></returns>
        // <CreateMediaServicesClient>
        public static async Task<IAzureMediaServicesClient> CreateMediaServicesClientAsync(MediaServiceConfigurationModel config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            // Authenticate to Azure.
            var azureServiceTokenProvider = new AzureServiceTokenProvider();
            var accessToken = await azureServiceTokenProvider.GetAccessTokenAsync("https://management.azure.com").ConfigureAwait(false);
            ServiceClientCredentials credentials = new TokenCredentials(accessToken);

            // Establish a connection to Media Services.
            return new AzureMediaServicesClient(credentials)
            {
                SubscriptionId = config.SubscriptionId
            };
        }

        public static async Task<Transform> EnsureTransformExists(IAzureMediaServicesClient client, string resourceGroupName, string accountName, string transformName, Preset preset)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            var transform = client.Transforms.Get(resourceGroupName, accountName, transformName);

            if (transform == null)
            {
                var outputs = new TransformOutput[]
                {
                    new TransformOutput(preset),
                };

                transform = await client.Transforms.CreateOrUpdateAsync(resourceGroupName, accountName, transformName, outputs).ConfigureAwait(false);
            }

            return transform;
        }

        public static async Task<ContentKeyPolicy> EnsureContentKeyPolicyExists(IAzureMediaServicesClient client, string resourceGroup, string accountName, string contentKeyPolicyName, byte[] tokenSigningKey, string issuer, string audience)
        {
            var primaryKey = new ContentKeyPolicySymmetricTokenKey(tokenSigningKey);
            List<ContentKeyPolicyRestrictionTokenKey> alternateKeys = null;
            var requiredClaims = new List<ContentKeyPolicyTokenClaim>()
            {
                ContentKeyPolicyTokenClaim.ContentKeyIdentifierClaim
            };

            var options = new List<ContentKeyPolicyOption>()
            {
                new ContentKeyPolicyOption(
                    new ContentKeyPolicyClearKeyConfiguration(),
                    new ContentKeyPolicyTokenRestriction(issuer, audience, primaryKey,
                        ContentKeyPolicyRestrictionTokenType.Jwt, alternateKeys, requiredClaims))
            };

            // since we are randomly generating the signing key each time, make sure to create or update the policy each time.
            // Normally you would use a long lived key so you would just check for the policies existence with Get instead of
            // ensuring to create or update it each time.
            var policy = await client.ContentKeyPolicies.CreateOrUpdateAsync(resourceGroup, accountName, contentKeyPolicyName, options).ConfigureAwait(false);

            return policy;
        }

        public static string GetToken(string issuer, string audience, string keyIdentifier, byte[] tokenVerificationKey)
        {
            var tokenSigningKey = new SymmetricSecurityKey(tokenVerificationKey);

            var cred = new SigningCredentials(
                tokenSigningKey,
                // Use the  HmacSha256 and not the HmacSha256Signature option, or the token will not work!
                SecurityAlgorithms.HmacSha256,
                SecurityAlgorithms.Sha256Digest);

            var claims = new Claim[]
            {
                new Claim(ContentKeyPolicyTokenClaim.ContentKeyIdentifierClaim.ClaimType, keyIdentifier)
            };

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                notBefore: DateTime.Now.AddMinutes(-5),
                expires: DateTime.Now.AddMinutes(60),
                signingCredentials: cred);

            var handler = new JwtSecurityTokenHandler();

            return handler.WriteToken(token);
        }

        public static bool IsSystemError(Job job, string jobOutputAssetName)
        {
            if (job.State == JobState.Error)
            {
                foreach (var jobOutput in job.Outputs)
                {
                    if (jobOutput is JobOutputAsset)
                    {
                        var jobOutputAsset = (JobOutputAsset)jobOutput;
                        if (jobOutputAsset.State == JobState.Error && jobOutputAsset.AssetName.Equals(jobOutputAssetName, StringComparison.InvariantCultureIgnoreCase))
                        {
                            if (jobOutputAsset.Error.Retry == JobRetry.MayRetry)
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        public static JobState GetJobOutputState(Job job, string jobOutputAssetName)
        {
            foreach (var jobOutput in job.Outputs)
            {
                if (jobOutput is JobOutputAsset)
                {
                    var jobOutputAsset = (JobOutputAsset)jobOutput;
                    if (jobOutputAsset.AssetName.Equals(jobOutputAssetName, StringComparison.InvariantCultureIgnoreCase))
                    {
                        return jobOutputAsset.State;
                    }
                }
            }

            return null;
        }

        public static bool IsSystemError(MediaJobOutputAsset jobOutput)
        {
            if (jobOutput.State == MediaJobState.Error)
            {
                if (jobOutput.Error.Retry == MediaJobRetry.MayRetry)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

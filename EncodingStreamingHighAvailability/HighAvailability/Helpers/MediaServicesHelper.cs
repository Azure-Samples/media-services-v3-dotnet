namespace HighAvailability.Helpers
{
    using HighAvailability.Interfaces;
    using HighAvailability.Models;
    using Microsoft.Azure.EventGrid.Models;
    using Microsoft.Azure.Management.Media;
    using Microsoft.Azure.Management.Media.Models;
    using Microsoft.Azure.Services.AppAuthentication;
    using Microsoft.Extensions.Logging;
    using Microsoft.IdentityModel.Tokens;
    using Microsoft.Rest;
    using Microsoft.Rest.Azure;
    using System;
    using System.Collections.Generic;
    using System.IdentityModel.Tokens.Jwt;
    using System.Security.Claims;
    using System.Threading.Tasks;

    /// <summary>
    /// Implements helper methods for Azure Media Services instance client
    /// </summary>
    public static class MediaServicesHelper
    {
        /// <summary>
        /// Creates the AzureMediaServicesClient object
        /// </summary>
        /// <param name="config">configuration data </param>
        /// <returns>Azure Media Services instance client</returns>
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

        /// <summary>
        /// Checks if transform exists, if not, creates transform
        /// </summary>
        /// <param name="client">Azure Media Services instance client</param>
        /// <param name="resourceGroupName">Azure resource group</param>
        /// <param name="accountName">Azure Media Services instance account name</param>
        /// <param name="transformName">Transform name</param>
        /// <param name="preset">transform preset object</param>
        /// <returns></returns>
        public static async Task<Transform> EnsureTransformExists(IAzureMediaServicesClient client, string resourceGroupName, string accountName, string transformName, Preset preset)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            // try to get existing transform
            var transform = client.Transforms.Get(resourceGroupName, accountName, transformName);

            // if transform does not exist
            if (transform == null)
            {
                // create output with given preset
                var outputs = new TransformOutput[]
                {
                    new TransformOutput(preset),
                };

                // create new transform
                transform = await client.Transforms.CreateOrUpdateAsync(resourceGroupName, accountName, transformName, outputs).ConfigureAwait(false);
            }

            return transform;
        }

        /// <summary>
        /// Checks if content key policy exists, if not, creates new one
        /// This code is based on https://github.com/Azure-Samples/media-services-v3-dotnet-core-tutorials/tree/master/NETCore/EncodeHTTPAndPublishAESEncrypted
        /// </summary>
        /// <param name="client">Azure Media Services instance client</param>
        /// <param name="resourceGroup">Azure resource group</param>
        /// <param name="accountName">Azure Media Services instance account name</param>
        /// <param name="contentKeyPolicyName">Content key policy name</param>
        /// <param name="tokenSigningKey">Token signing key</param>
        /// <param name="issuer">Token issuer</param>
        /// <param name="audience">Token audience</param>
        /// <returns></returns>
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

            var policy = await client.ContentKeyPolicies.CreateOrUpdateAsync(resourceGroup, accountName, contentKeyPolicyName, options).ConfigureAwait(false);

            return policy;
        }

        /// <summary>
        /// Gets token to for a given key identifier and key
        /// This code is based on https://github.com/Azure-Samples/media-services-v3-dotnet-core-tutorials/tree/master/NETCore/EncodeHTTPAndPublishAESEncrypted
        /// </summary>
        /// <param name="issuer">Token issuer</param>
        /// <param name="audience">Token audience</param>
        /// <param name="keyIdentifier">key identifier</param>
        /// <param name="tokenVerificationKey">binary key</param>
        /// <returns></returns>
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

        /// <summary>
        /// Determines if failed job should be resubmitted
        /// </summary>
        /// <param name="job">Azure Media Services job</param>
        /// <param name="jobOutputAssetName">Output asset name</param>
        /// <returns>true if job should be resubmitted</returns>
        public static bool IsSystemError(Job job, string jobOutputAssetName)
        {
            // if overall job has failed
            if (job.State == JobState.Error)
            {
                // find job output associated with specific asset name
                foreach (var jobOutput in job.Outputs)
                {
                    if (jobOutput is JobOutputAsset)
                    {
                        var jobOutputAsset = (JobOutputAsset)jobOutput;
                        if (jobOutputAsset.State == JobState.Error && jobOutputAsset.AssetName.Equals(jobOutputAssetName, StringComparison.InvariantCultureIgnoreCase))
                        {
                            // check if job should be retried
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

        /// <summary>
        /// Returns job state for specific asset
        /// </summary>
        /// <param name="job">Azure Media Services job</param>
        /// <param name="jobOutputAssetName">asset name</param>
        /// <returns>JobState for a given asset name</returns>
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

        /// <summary>
        /// Determines if failed job should be resubmitted using EventGrid event data
        /// </summary>
        /// <param name="jobOutput">Job output from EventGrid event</param>
        /// <returns>true if job should be resubmitted</returns>
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

        /// <summary>
        /// This method wraps the logic to record status of all the calls to Azure Media Services that return data.
        /// </summary>
        /// <typeparam name="TContext">Context type</typeparam>
        /// <typeparam name="TResult">Media Services return operation type</typeparam>
        /// <param name="func">Function to call</param>
        /// <param name="context">Operation context, in most cases this is a message that has triggered overall function. This is useful data if manual retries is required to reprocess messages</param>
        /// <param name="mediaServiceAccountName">Account name</param>
        /// <param name="mediaServiceCallHistoryStorageService">Storage service to persist call data</param>
        /// <param name="callName">Name of the call to Azure Media Services</param>
        /// <param name="logger">Logger to log data</param>
        /// <returns></returns>
        public static async Task<TResult> CallAzureMediaServices<TContext, TResult>(
            Func<Task<AzureOperationResponse<TResult>>> func,
            TContext context,
            string mediaServiceAccountName,
            IMediaServiceCallHistoryStorageService mediaServiceCallHistoryStorageService,
            string callName,
            ILogger logger)
        {
            AzureOperationResponse<TResult> result;

            result = await CallAzureMediaServiceInternal(func, context, mediaServiceAccountName, mediaServiceCallHistoryStorageService, callName, logger).ConfigureAwait(false);

            return result.Body;
        }

        /// <summary>
        /// This method wraps the logic to record status of all the calls to Azure Media Services that do not return data.
        /// </summary>
        /// <typeparam name="TContext">Context type</typeparam>
        /// <param name="func">Function to call</param>
        /// <param name="context">Operation context, in most cases this is a message that has triggered overall function. This is useful data if manual retries is required to reprocess messages</param>
        /// <param name="mediaServiceAccountName">account name</param>
        /// <param name="mediaServiceCallHistoryStorageService">storage service to persist call data</param>
        /// <param name="callName">name of the call to Azure Media Services</param>
        /// <param name="logger">Logger to log data</param>
        /// <returns></returns>
        public static async Task CallAzureMediaServices<TContext>(
            Func<Task<AzureOperationResponse>> func,
            TContext context,
            string mediaServiceAccountName,
            IMediaServiceCallHistoryStorageService mediaServiceCallHistoryStorageService,
            string callName,
            ILogger logger)
        {
            await CallAzureMediaServiceInternal(func, context, mediaServiceAccountName, mediaServiceCallHistoryStorageService, callName, logger).ConfigureAwait(false);
        }

        /// <summary>
        /// This method wraps the logic to record status of all the calls to Azure Media Services
        /// </summary>
        /// <typeparam name="TContext">Context type</typeparam>
        /// <typeparam name="TResult">Http response type implementing IAzureOperationResponse</typeparam>
        /// <param name="func">Function to call</param>
        /// <param name="context">Operation context, in most cases this is a message that has triggered overall function. This is useful data if manual retries is required to reprocess messages</param>
        /// <param name="mediaServiceAccountName">Account name</param>
        /// <param name="mediaServiceCallHistoryStorageService">Storage service to persist call data</param>
        /// <param name="callName">Name of the call to Azure Media Services</param>
        /// <param name="logger">Logger to log data</param>
        /// <returns></returns>
        private static async Task<TResult> CallAzureMediaServiceInternal<TContext, TResult>(Func<Task<TResult>> func, TContext context, string mediaServiceAccountName, IMediaServiceCallHistoryStorageService mediaServiceCallHistoryStorageService, string callName, ILogger logger) where TResult : IAzureOperationResponse
        {
            TResult result;

            // Create model and initiliaze common field values
            var mediaServiceCallHistoryModel = new MediaServiceCallHistoryModel
            {
                Id = Guid.NewGuid().ToString(),
                MediaServiceAccountName = mediaServiceAccountName,
                CallName = callName,
            };

            mediaServiceCallHistoryModel.SetContextData(context);

            // if call fails, need to log the result
            try
            {
                result = await func().ConfigureAwait(false);
            }
            catch (ApiErrorException e)
            {
                // log failed call data
                mediaServiceCallHistoryModel.HttpStatus = e.Response.StatusCode;
                mediaServiceCallHistoryModel.EventTime = DateTime.UtcNow;
                await mediaServiceCallHistoryStorageService.CreateOrUpdateAsync(mediaServiceCallHistoryModel, logger).ConfigureAwait(false);
                throw;
            }

            // log successful call
            mediaServiceCallHistoryModel.EventTime = DateTime.UtcNow;
            mediaServiceCallHistoryModel.HttpStatus = result.Response.StatusCode;

            // In order to keep this operation idempotent, there is no need to fail even if recording call data fails. Otherwise when request is resubmitted and it can result in data duplication.
            var retryCount = 3;
            var retryTimeOut = 1000;

            do
            {
                try
                {
                    // try to store data
                    await mediaServiceCallHistoryStorageService.CreateOrUpdateAsync(mediaServiceCallHistoryModel, logger).ConfigureAwait(false);

                    // no exception, break
                    break;
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception e)
#pragma warning restore CA1031 // Do not catch general exception types
                {
                    logger.LogError($"MediaServicesHelper::CallAzureMediaServices got exception calling mediaServiceCallHistoryStorageService.CreateOrUpdateAsync: retryCount={retryCount} message={e.Message} mediaServiceCallHistoryModel={LogHelper.FormatObjectForLog(mediaServiceCallHistoryModel)}");
                    retryCount--;
                    await Task.Delay(retryTimeOut).ConfigureAwait(false);
                }
            }
            while (retryCount > 0);

            return result;
        }
    }
}

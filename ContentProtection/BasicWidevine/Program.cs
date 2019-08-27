// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Azure.EventHubs;
using Microsoft.Azure.EventHubs.Processor;
using Microsoft.Azure.Management.Media;
using Microsoft.Azure.Management.Media.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Rest;
using Microsoft.Rest.Azure.Authentication;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace BasicWidevine
{
    class Program
    {
        private const string AdaptiveStreamingTransformName = "MyTransformWithAdaptiveStreamingPreset";
        private static readonly string Issuer = "myIssuer";
        private static readonly string Audience = "myAudience";
        private static byte[] TokenSigningKey = new byte[40];
        private static readonly string ContentKeyPolicyName = "WidevineContentKeyPolicy";
        private static readonly string DefaultStreamingEndpointName = "se";     // Change this to your Endpoint name.

        public static async Task Main(string[] args)
        {
            // Please make sure you have set configuration in appsettings.json.For more information, see
            // https://docs.microsoft.com/azure/media-services/latest/access-api-cli-how-to.
            ConfigWrapper config = new ConfigWrapper(new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build());

            try
            {
                await RunAsync(config);
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"{exception.Message}");

                if (exception.GetBaseException() is ApiErrorException apiException)
                {
                    Console.Error.WriteLine(
                        $"ERROR: API call failed with error code '{apiException.Body.Error.Code}' and message '{apiException.Body.Error.Message}'.");
                }
            }

            Console.WriteLine("Press Enter to continue.");
            Console.ReadLine();
        }

        /// <summary>
        /// Run the sample async.
        /// </summary>
        /// <param name="config">The param is of type ConfigWrapper. This class reads values from local configuration file.</param>
        /// <returns></returns>
        private static async Task RunAsync(ConfigWrapper config)
        {
            IAzureMediaServicesClient client;
            try
            {
                client = await CreateMediaServicesClientAsync(config);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("TIP: Make sure that you have filled out the appsettings.json file before running this sample.");
                Console.Error.WriteLine($"{e.Message}");
                return;
            }

            // Set the polling interval for long running operations to 2 seconds.
            // The default value is 30 seconds for the .NET client SDK
            client.LongRunningOperationRetryTimeout = 2;

            // Creating a unique suffix so that we don't have name collisions if you run the sample
            // multiple times without cleaning up.
            string uniqueness = Guid.NewGuid().ToString("N");
            string jobName = $"job-{uniqueness}";
            string locatorName = $"locator-{uniqueness}";
            string outputAssetName = $"output-{uniqueness}";
            bool stopEndpoint = false;
            EventProcessorHost eventProcessorHost = null;

            try
            {
                // Ensure that you have the desired encoding Transform. This is really a one time setup operation.
                Transform transform = await GetOrCreateTransformAsync(client, config.ResourceGroup, config.AccountName, AdaptiveStreamingTransformName);

                // Output from the encoding Job must be written to an Asset, so let's create one
                Asset outputAsset = await CreateOutputAssetAsync(client, config.ResourceGroup, config.AccountName, outputAssetName);

                Job job = await SubmitJobAsync(client, config.ResourceGroup, config.AccountName, AdaptiveStreamingTransformName, outputAsset.Name, jobName);

                try
                {
                    // First we will try to process Job events through Event Hub in real-time. If this fails for any reason,
                    // we will fall-back on polling Job status instead.

                    // Please refer README for Event Hub and storage settings.
                    string StorageConnectionString = string.Format("DefaultEndpointsProtocol=https;AccountName={0};AccountKey={1}",
                        config.StorageAccountName, config.StorageAccountKey);

                    // Create a new host to process events from an Event Hub.
                    Console.WriteLine("Creating a new host to process events from an Event Hub...");
                    eventProcessorHost = new EventProcessorHost(config.EventHubName,
                        PartitionReceiver.DefaultConsumerGroupName, config.EventHubConnectionString,
                        StorageConnectionString, config.StorageContainerName);

                    // Create an AutoResetEvent to wait for the job to finish and pass it to EventProcessor so that it can be set when a final state event is received.
                    AutoResetEvent jobWaitingEvent = new AutoResetEvent(false);

                    // Registers the Event Processor Host and starts receiving messages. Pass in jobWaitingEvent so it can be called back.
                    await eventProcessorHost.RegisterEventProcessorFactoryAsync(new MediaServicesEventProcessorFactory(jobName, jobWaitingEvent),
                        EventProcessorOptions.DefaultOptions);

                    // Create a Task list, adding a job waiting task and a timer task. Other tasks can be added too.
                    IList<Task> tasks = new List<Task>();

                    // Add a task to wait for the job to finish. jobWaitingEvent will be set when a final state is received by EventProcessor.
                    Task jobTask = Task.Run(() => jobWaitingEvent.WaitOne());
                    tasks.Add(jobTask);

                    // 30 minutes timeout.
                    var tokenSource = new CancellationTokenSource();
                    var timeout = Task.Delay(30 * 60 * 1000, tokenSource.Token);
                    tasks.Add(timeout);

                    // Wait for any task to finish.
                    if (await Task.WhenAny(tasks) == jobTask)
                    {
                        // Job finished. Cancel the timer.
                        tokenSource.Cancel();
                        // Get the latest status of the job.
                        job = await client.Jobs.GetAsync(config.ResourceGroup, config.AccountName, AdaptiveStreamingTransformName, jobName);
                    }
                    else
                    {
                        // Timeout happened, Something might go wrong with job events. Fall-back on polling instead.
                        jobWaitingEvent.Set();
                        throw new Exception("Timeout happened.");
                    }
                }
                catch (Exception)
                {
                    Console.WriteLine("Warning: Failed to connect to Event Hub, please refer README for Event Hub and storage settings.");

                    // Polling is not a recommended best practice for production applications because of the latency it introduces.
                    // Overuse of this API may trigger throttling. Developers should instead use Event Grid.
                    Console.WriteLine("Polling job status...");
                    job = await WaitForJobToFinishAsync(client, config.ResourceGroup, config.AccountName, AdaptiveStreamingTransformName, jobName);
                }

                if (job.State == JobState.Finished)
                {
                    // Set a token signing key that you want to use
                    TokenSigningKey = Convert.FromBase64String(config.SymmetricKey);

                    // Create the content key policy that configures how the content key is delivered to end clients
                    // via the Key Delivery component of Azure Media Services.
                    // We are using the ContentKeyIdentifierClaim in the ContentKeyPolicy which means that the token presented
                    // to the Key Delivery Component must have the identifier of the content key in it. 
                    ContentKeyPolicy policy = await GetOrCreateContentKeyPolicyAsync(client, config.ResourceGroup, config.AccountName, ContentKeyPolicyName, TokenSigningKey);

                    // Sets StreamingLocator.StreamingPolicyName to "Predefined_MultiDrmCencStreaming" policy.
                    StreamingLocator locator = await CreateStreamingLocatorAsync(client, config.ResourceGroup, config.AccountName, outputAsset.Name, locatorName, ContentKeyPolicyName);

                    // In this example, we want to play the Widevine (CENC) encrypted stream. 
                    // We need to get the key identifier of the content key where its type is CommonEncryptionCenc.
                    string keyIdentifier = locator.ContentKeys.Where(k => k.Type == StreamingLocatorContentKeyType.CommonEncryptionCenc).First().Id.ToString();

                    Console.WriteLine($"KeyIdentifier = {keyIdentifier}");

                    // In order to generate our test token we must get the ContentKeyId to put in the ContentKeyIdentifierClaim claim.
                    string token = GetTokenAsync(Issuer, Audience, keyIdentifier, TokenSigningKey);

                    StreamingEndpoint streamingEndpoint = await client.StreamingEndpoints.GetAsync(config.ResourceGroup, config.AccountName,
                        DefaultStreamingEndpointName);

                    if (streamingEndpoint != null)
                    {
                        if (streamingEndpoint.ResourceState != StreamingEndpointResourceState.Running)
                        {
                            Console.WriteLine("Streaming Endpoint was Stopped, restarting now..");
                            await client.StreamingEndpoints.StartAsync(config.ResourceGroup, config.AccountName, DefaultStreamingEndpointName);

                            // Since we started the endpoint, we should stop it in cleanup.
                            stopEndpoint = true;
                        }
                    }
                    string dashPath = await GetDASHStreamingUrlAsync(client, config.ResourceGroup, config.AccountName, locator.Name, streamingEndpoint);

                    Console.WriteLine("Copy and paste the following URL in your browser to play back the file in the Azure Media Player.");
                    Console.WriteLine("You can use Chrome/Firefox for Widevine.");
                    Console.WriteLine();
                    Console.WriteLine($"https://ampdemo.azureedge.net/?url={dashPath}&widevine=true&token=Bearer%3D{token}");
                    Console.WriteLine();
                }

                Console.WriteLine("When finished testing press enter to cleanup.");
                Console.Out.Flush();
                Console.ReadLine();
            }
            catch (ApiErrorException e)
            {
                Console.WriteLine("Hit ApiErrorException");
                Console.WriteLine($"\tCode: {e.Body.Error.Code}");
                Console.WriteLine($"\tMessage: {e.Body.Error.Message}");
                Console.WriteLine();
                Console.WriteLine("Exiting, cleanup may be necessary...");
                Console.ReadLine();
            }
            finally
            {
                Console.WriteLine("Cleaning up...");
                await CleanUpAsync(client, config.ResourceGroup, config.AccountName, AdaptiveStreamingTransformName, outputAssetName,
                    jobName, ContentKeyPolicyName, stopEndpoint, DefaultStreamingEndpointName);

                if (eventProcessorHost != null)
                {
                    Console.WriteLine("Unregistering event processor...");

                    // Disposes of the Event Processor Host.
                    await eventProcessorHost.UnregisterEventProcessorAsync();
                    Console.WriteLine();
                }
            }
        }

        /// <summary>
        /// Create the ServiceClientCredentials object based on the credentials
        /// supplied in local configuration file.
        /// </summary>
        /// <param name="config">The param is of type ConfigWrapper. This class reads values from local configuration file.</param>
        /// <returns></returns>
        private static async Task<ServiceClientCredentials> GetCredentialsAsync(ConfigWrapper config)
        {
            // Use ApplicationTokenProvider.LoginSilentAsync to get a token using a service principal with symmetric key
            ClientCredential clientCredential = new ClientCredential(config.AadClientId, config.AadSecret);
            return await ApplicationTokenProvider.LoginSilentAsync(config.AadTenantId, clientCredential, ActiveDirectoryServiceSettings.Azure);
        }

        /// <summary>
        /// Creates the AzureMediaServicesClient object based on the credentials
        /// supplied in local configuration file.
        /// </summary>
        /// <param name="config">The param is of type ConfigWrapper. This class reads values from local configuration file.</param>
        /// <returns></returns>
        private static async Task<IAzureMediaServicesClient> CreateMediaServicesClientAsync(ConfigWrapper config)
        {
            var credentials = await GetCredentialsAsync(config);

            return new AzureMediaServicesClient(config.ArmEndpoint, credentials)
            {
                SubscriptionId = config.SubscriptionId,
            };
        }

        /// <summary>
        /// Create the content key policy that configures how the content key is delivered to end clients 
        /// via the Key Delivery component of Azure Media Services.
        /// </summary>
        /// <param name="client">The Media Services client.</param>
        /// <param name="resourceGroupName">The name of the resource group within the Azure subscription.</param>
        /// <param name="accountName"> The Media Services account name.</param>
        /// <param name="contentKeyPolicyName">The name of the content key policy resource.</param>
        /// <returns></returns>
        private static async Task<ContentKeyPolicy> GetOrCreateContentKeyPolicyAsync(
            IAzureMediaServicesClient client,
            string resourceGroupName,
            string accountName,
            string contentKeyPolicyName,
            byte[] tokenSigningKey)
        {
            ContentKeyPolicy policy = await client.ContentKeyPolicies.GetAsync(resourceGroupName, accountName, contentKeyPolicyName);

            if (policy == null)
            {
                ContentKeyPolicySymmetricTokenKey primaryKey = new ContentKeyPolicySymmetricTokenKey(tokenSigningKey);
                List<ContentKeyPolicyTokenClaim> requiredClaims = new List<ContentKeyPolicyTokenClaim>()
                {
                    ContentKeyPolicyTokenClaim.ContentKeyIdentifierClaim
                };
                List<ContentKeyPolicyRestrictionTokenKey> alternateKeys = null;
                ContentKeyPolicyTokenRestriction restriction
                    = new ContentKeyPolicyTokenRestriction(Issuer, Audience, primaryKey, ContentKeyPolicyRestrictionTokenType.Jwt, alternateKeys, requiredClaims);

                ContentKeyPolicyWidevineConfiguration widevineConfig = ConfigureWidevineLicenseTempate();

                List<ContentKeyPolicyOption> options = new List<ContentKeyPolicyOption>
                {
                    new ContentKeyPolicyOption()
                    {
                        Configuration = widevineConfig,
                        Restriction = restriction
                    }
                };

                policy = await client.ContentKeyPolicies.CreateOrUpdateAsync(resourceGroupName, accountName, contentKeyPolicyName, options);
            }
            else
            {
                // Get the signing key from the existing policy.
                var policyProperties = await client.ContentKeyPolicies.GetPolicyPropertiesWithSecretsAsync(resourceGroupName, accountName, contentKeyPolicyName);
                if (policyProperties.Options[0].Restriction is ContentKeyPolicyTokenRestriction restriction)
                {
                    if (restriction.PrimaryVerificationKey is ContentKeyPolicySymmetricTokenKey signingKey)
                    {
                        TokenSigningKey = signingKey.KeyValue;
                    }
                }
            }
            return policy;
        }

        /// <summary>
        /// If the specified transform exists, get that transform.
        /// If the it does not exist, creates a new transform with the specified output. 
        /// In this case, the output is set to encode a video using one of the built-in encoding presets.
        /// </summary>
        /// <param name="client">The Media Services client.</param>
        /// <param name="resourceGroupName">The name of the resource group within the Azure subscription.</param>
        /// <param name="accountName"> The Media Services account name.</param>
        /// <param name="transformName">The name of the transform.</param>
        /// <returns></returns>
        private static async Task<Transform> GetOrCreateTransformAsync(
            IAzureMediaServicesClient client,
            string resourceGroupName,
            string accountName,
            string transformName)
        {
            // Does a Transform already exist with the desired name? Assume that an existing Transform with the desired name
            // also uses the same recipe or Preset for processing content.
            Transform transform = await client.Transforms.GetAsync(resourceGroupName, accountName, transformName);

            if (transform == null)
            {
                // You need to specify what you want it to produce as an output
                TransformOutput[] output = new TransformOutput[]
                {
                    new TransformOutput
                    {
                        // The preset for the Transform is set to one of Media Services built-in sample presets.
                        // You can  customize the encoding settings by changing this to use "StandardEncoderPreset" class.
                        Preset = new BuiltInStandardEncoderPreset()
                        {
                            // This sample uses the built-in encoding preset for Adaptive Bitrate Streaming.
                            PresetName = EncoderNamedPreset.AdaptiveStreaming
                        }
                    }
                };

                // Create the Transform with the output defined above
                Console.WriteLine("Creating a transform...");
                transform = await client.Transforms.CreateOrUpdateAsync(resourceGroupName, accountName, transformName, output);
            }

            return transform;
        }


        /// <summary>
        /// Creates an output asset. The output from the encoding Job must be written to an Asset.
        /// </summary>
        /// <param name="client">The Media Services client.</param>
        /// <param name="resourceGroupName">The name of the resource group within the Azure subscription.</param>
        /// <param name="accountName"> The Media Services account name.</param>
        /// <param name="assetName">The output asset name.</param>
        /// <returns></returns>
        private static async Task<Asset> CreateOutputAssetAsync(IAzureMediaServicesClient client, string resourceGroupName, string accountName, string assetName)
        {
            // Check if an Asset already exists
            Asset outputAsset = await client.Assets.GetAsync(resourceGroupName, accountName, assetName);
            Asset asset = new Asset();
            string outputAssetName = assetName;

            if (outputAsset != null)
            {
                // Name collision! In order to get the sample to work, let's just go ahead and create a unique asset name
                // Note that the returned Asset can have a different name than the one specified as an input parameter.
                // You may want to update this part to throw an Exception instead, and handle name collisions differently.
                string uniqueness = $"-{Guid.NewGuid().ToString("N")}";
                outputAssetName += uniqueness;

                Console.WriteLine("Warning – found an existing Asset with name = " + assetName);
                Console.WriteLine("Creating an Asset with this name instead: " + outputAssetName);
            }

            Console.WriteLine("Creating an output asset...");
            return await client.Assets.CreateOrUpdateAsync(resourceGroupName, accountName, outputAssetName, asset);
        }

        /// <summary>
        /// Submits a request to Media Services to apply the specified Transform to a given input video.
        /// </summary>
        /// <param name="client">The Media Services client.</param>
        /// <param name="resourceGroupName">The name of the resource group within the Azure subscription.</param>
        /// <param name="accountName"> The Media Services account name.</param>
        /// <param name="transformName">The name of the transform.</param>
        /// <param name="outputAssetName">The (unique) name of the  output asset that will store the result of the encoding job. </param>
        /// <param name="jobName">The (unique) name of the job.</param>
        /// <returns></returns>
        private static async Task<Job> SubmitJobAsync(IAzureMediaServicesClient client,
            string resourceGroup,
            string accountName,
            string transformName,
            string outputAssetName,
            string jobName)
        {
            // This example shows how to encode from any HTTPs source URL - a new feature of the v3 API.  
            // Change the URL to any accessible HTTPs URL or SAS URL from Azure.
            JobInputHttp jobInput =
                new JobInputHttp(files: new[] { "https://nimbuscdn-nimbuspm.streaming.mediaservices.windows.net/2b533311-b215-4409-80af-529c3e853622/Ignite-short.mp4" });

            JobOutput[] jobOutputs =
            {
                new JobOutputAsset(outputAssetName),
            };

            // In this example, we are assuming that the job name is unique.
            //
            // If you already have a job with the desired name, use the Jobs.Get method
            // to get the existing job. In Media Services v3, the Get method on entities returns null 
            // if the entity doesn't exist (a case-insensitive check on the name).
            Console.WriteLine("Creating a job...");
            Job job = await client.Jobs.CreateAsync(
                resourceGroup,
                accountName,
                transformName,
                jobName,
                new Job
                {
                    Input = jobInput,
                    Outputs = jobOutputs,
                });

            return job;
        }


        /// <summary>
        /// Polls Media Services for the status of the Job.
        /// </summary>
        /// <param name="client">The Media Services client.</param>
        /// <param name="resourceGroupName">The name of the resource group within the Azure subscription.</param>
        /// <param name="accountName"> The Media Services account name.</param>
        /// <param name="transformName">The name of the transform.</param>
        /// <param name="jobName">The name of the job you submitted.</param>
        /// <returns></returns>
        private static async Task<Job> WaitForJobToFinishAsync(IAzureMediaServicesClient client,
            string resourceGroupName,
            string accountName,
            string transformName,
            string jobName)
        {
            const int SleepIntervalMs = 60 * 1000;

            Job job;

            do
            {
                job = await client.Jobs.GetAsync(resourceGroupName, accountName, transformName, jobName);

                Console.WriteLine($"Job is '{job.State}'.");
                for (int i = 0; i < job.Outputs.Count; i++)
                {
                    JobOutput output = job.Outputs[i];
                    Console.Write($"\tJobOutput[{i}] is '{output.State}'.");
                    if (output.State == JobState.Processing)
                    {
                        Console.Write($"  Progress: '{output.Progress}'.");
                    }

                    Console.WriteLine();
                }

                if (job.State != JobState.Finished && job.State != JobState.Error && job.State != JobState.Canceled)
                {
                    await Task.Delay(SleepIntervalMs);
                }
            }
            while (job.State != JobState.Finished && job.State != JobState.Error && job.State != JobState.Canceled);

            return job;
        }

        /// <summary>
        /// Configures Widevine license template.
        /// </summary>
        /// <returns></returns>
        private static ContentKeyPolicyWidevineConfiguration ConfigureWidevineLicenseTempate()
        {
            WidevineTemplate template = new WidevineTemplate()
            {
                AllowedTrackTypes = "SD_HD",
                ContentKeySpecs = new ContentKeySpec[]
                {
                    new ContentKeySpec()
                    {
                        TrackType = "SD",
                        SecurityLevel = 1,
                        RequiredOutputProtection = new OutputProtection()
                        {
                            HDCP = "HDCP_NONE"
                        }
                    }
                },
                PolicyOverrides = new PolicyOverrides()
                {
                    CanPlay = true,
                    CanPersist = false,
                    CanRenew = false,
                    RentalDurationSeconds = 2592000,
                    PlaybackDurationSeconds = 10800,
                    LicenseDurationSeconds = 604800,
                }
            };

            ContentKeyPolicyWidevineConfiguration objContentKeyPolicyWidevineConfiguration = new ContentKeyPolicyWidevineConfiguration
            {
                WidevineTemplate = Newtonsoft.Json.JsonConvert.SerializeObject(template)
            };
            return objContentKeyPolicyWidevineConfiguration;
        }

        /// <summary>
        /// Creates a StreamingLocator for the specified asset and with the specified streaming policy name.
        /// Once the StreamingLocator is created the output asset is available to clients for playback.
        /// 
        /// This StreamingLocator uses "Predefined_MultiDrmCencStreaming" 
        /// because this sample encrypts with Widevine (CENC encryption).  
        /// "Predefined_MultiDrmCencStreaming" policy also adds AES encryption.
        /// As a result, two content keys are added to the StreamingLocator.
        /// 
        /// </summary>
        /// <param name="client">The Media Services client.</param>
        /// <param name="resourceGroupName">The name of the resource group within the Azure subscription.</param>
        /// <param name="accountName"> The Media Services account name.</param>
        /// <param name="assetName">The name of the output asset.</param>
        /// <param name="locatorName">The StreamingLocator name (unique in this case).</param>
        /// <returns></returns>
        private static async Task<StreamingLocator> CreateStreamingLocatorAsync(
            IAzureMediaServicesClient client,
            string resourceGroup,
            string accountName,
            string assetName,
            string locatorName,
            string contentPolicyName)
        {
            StreamingLocator locator = await client.StreamingLocators.GetAsync(resourceGroup, accountName, locatorName);

            if (locator != null)
            {
                // Name collision! This should not happen in this sample. If it does happen, in order to get the sample to work,
                // let's just go ahead and create a unique name.
                // Note that the returned locatorName can have a different name than the one specified as an input parameter.
                // You may want to update this part to throw an Exception instead, and handle name collisions differently.
                Console.WriteLine("Warning – found an existing Streaming Locator with name = " + locatorName);

                string uniqueness = $"-{Guid.NewGuid().ToString("N")}";
                locatorName += uniqueness;

                Console.WriteLine("Creating a Streaming Locator with this name instead: " + locatorName);
            }

            locator = await client.StreamingLocators.CreateAsync(
                resourceGroup,
                accountName,
                locatorName,
                new StreamingLocator
                {
                    AssetName = assetName,
                    // "Predefined_MultiDrmCencStreaming" policy supports envelope and cenc encryption
                    StreamingPolicyName = "Predefined_MultiDrmCencStreaming",
                    DefaultContentKeyPolicyName = contentPolicyName
                });

            return locator;
        }

        /// <summary>
        /// Create a token that will be used to protect your stream.
        /// Only authorized clients would be able to play the video.  
        /// </summary>
        /// <param name="issuer">The issuer is the secure token service that issues the token. </param>
        /// <param name="audience">The audience, sometimes called scope, describes the intent of the token or the resource the token authorizes access to. </param>
        /// <param name="keyIdentifier">The content key ID.</param>
        /// <param name="tokenVerificationKey">Contains the key that the token was signed with. </param>
        /// <returns></returns>
        private static string GetTokenAsync(string issuer, string audience, string keyIdentifier, byte[] tokenVerificationKey)
        {
            var tokenSigningKey = new SymmetricSecurityKey(tokenVerificationKey);

            SigningCredentials cred = new SigningCredentials(
                tokenSigningKey,
                // Use the  HmacSha256 and not the HmacSha256Signature option, or the token will not work!
                SecurityAlgorithms.HmacSha256,
                SecurityAlgorithms.Sha256Digest);

            Claim[] claims = new Claim[]
            {
                new Claim(ContentKeyPolicyTokenClaim.ContentKeyIdentifierClaim.ClaimType, keyIdentifier)
            };

            JwtSecurityToken token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                notBefore: DateTime.Now.AddMinutes(-5),
                expires: DateTime.Now.AddMinutes(60),
                signingCredentials: cred);

            JwtSecurityTokenHandler handler = new JwtSecurityTokenHandler();

            return handler.WriteToken(token);
        }

        /// <summary>
        /// Checks if the "default" streaming endpoint is in the running state,
        /// if not, starts it.
        /// Then, builds the streaming URLs.
        /// </summary>
        /// <param name="client">The Media Services client.</param>
        /// <param name="resourceGroupName">The name of the resource group within the Azure subscription.</param>
        /// <param name="accountName"> The Media Services account name.</param>
        /// <param name="locatorName">The name of the StreamingLocator that was created.</param>
        /// <param name="streamingEndpoint">The streaming endpoint.</param>
        /// <returns></returns>
        private static async Task<string> GetDASHStreamingUrlAsync(
            IAzureMediaServicesClient client,
            string resourceGroupName,
            string accountName,
            string locatorName,
            StreamingEndpoint streamingEndpoint)
        {
            string dashPath = "";

            ListPathsResponse paths = await client.StreamingLocators.ListPathsAsync(resourceGroupName, accountName, locatorName);

            foreach (StreamingPath path in paths.StreamingPaths)
            {
                UriBuilder uriBuilder = new UriBuilder
                {
                    Scheme = "https",
                    Host = streamingEndpoint.HostName
                };

                // Look for just the DASH path and generate a URL for the Azure Media Player to playback the encrypted DASH content. 
                // Note that the JWT token is set to expire in 1 hour. 
                if (path.StreamingProtocol == StreamingPolicyStreamingProtocol.Dash)
                {
                    uriBuilder.Path = path.Paths[0];
                    dashPath = uriBuilder.ToString();
                }
            }

            return dashPath;
        }

        /// <summary>
        /// Deletes the jobs and assets that were created.
        /// Generally, you should clean up everything except objects 
        /// that you are planning to reuse (typically, you will reuse Transforms, and you will persist StreamingLocators).
        /// </summary>
        /// <param name="client">The Media Services client.</param>
        /// <param name="resourceGroupName">The name of the resource group within the Azure subscription.</param>
        /// <param name="accountName"> The Media Services account name.</param>
        /// <param name="transformName">The transform name.</param>
        /// <param name="assetName">The output asset name</param>
        /// <param name="jobName">The job name.</param>
        /// <param name="contentKeyPolicyName">The content key policy name.</param>
        /// <param name="stopEndpoint">Stop endpoint if true, keep endpoint running if false.</param>
        /// <param name="streamingEndpointName">The endpoint name.</param>
        private static async Task CleanUpAsync(
            IAzureMediaServicesClient client,
            string resourceGroupName,
            string accountName,
            string transformName,
            string assetName,
            string jobName,
            string contentKeyPolicyName,
            bool stopEndpoint,
            string streamingEndpointName
            )
        {
            await client.Assets.DeleteAsync(resourceGroupName, accountName, assetName);

            await client.Jobs.DeleteAsync(resourceGroupName, accountName, transformName, jobName);

            await client.ContentKeyPolicies.DeleteAsync(resourceGroupName, accountName, contentKeyPolicyName);

            if (stopEndpoint)
            {
                // Because we started the endpoint, we'll stop it.
                await client.StreamingEndpoints.StopAsync(resourceGroupName, accountName, streamingEndpointName);
            }
            else
            {
                // We will keep the endpoint running because it was not started by us. There are costs to keep it running.
                // Please refer https://azure.microsoft.com/en-us/pricing/details/media-services/ for pricing. 
                Console.WriteLine($"The endpoint {streamingEndpointName} is running. To halt further billing on the endpoint, please stop it in azure portal or AMS Explorer.");
            }
        }
    }
}

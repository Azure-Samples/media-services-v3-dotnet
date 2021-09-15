using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using Microsoft.WindowsAzure.MediaServices.Client; // v2Client
using Microsoft.WindowsAzure.MediaServices.Client.ContentKeyAuthorization; // v2Client
using Microsoft.WindowsAzure.MediaServices.Client.DynamicEncryption; // v2Client
using Microsoft.Azure.Management.Media; // v3Client
using Microsoft.Azure.Management.Media.Models; // v3Client models
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Rest.Azure.Authentication;

namespace MigrationSample
{
    class Program
    {
        const int c_GuidLength = 36; // "CEEAC448-0658-45A0-9D7B-BB65E49162F8".Length

        /// <summary>
        /// V2 used identifier strings composed of a prefix that told what type of identifier it was (Asset, ContentKey, etc)
        /// and a globally unique identifier (GUID/UUID).  V3 is based on Azure Resource Manager which uses identifier strings
        /// based on the full path in ARM which includes the subscription Id (which is set on the v3Client on creation), the
        /// resource group, the media services account name, the entity type, and a unique name for the entity.  Assets in V2
        /// had a Name property and but did not enforce uniqueness so it cannot be used for the unique name for the entity in V3.
        /// Thus, the GUID/UUID part of the identifer is used as the name and this method takes the V2 Asset Identifier and returns
        /// the name used in V3.
        /// </summary>
        /// <param name="identifier"></param>
        /// <returns></returns>
        static string Getv3AssetNameFromv2Asset(IAsset asset)
        {                        
            int startIndex = asset.Id.Length - c_GuidLength;
            return asset.Id.Substring(startIndex);
        }

        static CloudMediaContext GetV2Client(ConfigWrapper config)
        {
            AzureAdTokenCredentials tokenCredentials = new AzureAdTokenCredentials(config.AadTenantDomain, new AzureAdClientSymmetricKey(config.AadClientId, config.AadSecret), AzureEnvironments.AzureCloudEnvironment);
            AzureAdTokenProvider tokenProvider = new AzureAdTokenProvider(tokenCredentials);

            return new CloudMediaContext(config.AmsRestApiEndpoint, tokenProvider);
        }

        private static IMediaProcessor GetMediaEncoderStandardProcessor(CloudMediaContext v2Client)
        {
            return v2Client.MediaProcessors.Where(p => p.Name == "Media Encoder Standard").ToList().OrderBy(p => new Version(p.Version)).Last();
        }

        static private byte[] GetRandomBuffer(int size)
        {
            byte[] randomBytes = new byte[size];
            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(randomBytes);
            }

            return randomBytes;
        }

        static IAsset CreateAssetAndProtectedStreamingLocatorInV2(CloudMediaContext v2Client, ConfigWrapper config, string assetNameOrDescription)
        {
            // Create the input Asset
            IAsset originalAsset = v2Client.Assets.Create("input asset", AssetCreationOptions.None);
            string filename = Path.GetFileName(config.FilePathToUpload);
            IAssetFile assetFile = originalAsset.AssetFiles.Create(filename);
            assetFile.Upload(config.FilePathToUpload);

            // Submit a job to encode the single input file into an adaptive streaming set
            IJob job = v2Client.Jobs.Create("Media Encoder Standard Job");
            IMediaProcessor processor = GetMediaEncoderStandardProcessor(v2Client);

            ITask task = job.Tasks.AddNew("Adaptive Streaming encode", processor, "Adaptive Streaming", TaskOptions.None);
            task.InputAssets.Add(originalAsset);
            task.OutputAssets.AddNew(assetNameOrDescription, AssetCreationOptions.None);

            job.Submit();
            job.GetExecutionProgressTask(CancellationToken.None).Wait();

            // Get the output asset to publish
            job.Refresh();
            IAsset assetToPublish = v2Client.Assets.Where(a => a.Id == job.Tasks[0].OutputAssets[0].Id).First();

            // Create the content key
            Guid keyId = Guid.NewGuid();
            byte[] contentKey = GetRandomBuffer(16);

            IContentKey key = v2Client.ContentKeys.Create(keyId, contentKey, "ContentKey", ContentKeyType.EnvelopeEncryption);

            // Create ContentKeyAuthorizationPolicy with Open restriction and create authorization policy
            IContentKeyAuthorizationPolicy policy = v2Client.ContentKeyAuthorizationPolicies.CreateAsync("Open Authorization Policy").Result;

            ContentKeyAuthorizationPolicyRestriction restriction =
                new ContentKeyAuthorizationPolicyRestriction
                {
                    Name = "Open Authorization Policy",
                    KeyRestrictionType = (int)ContentKeyRestrictionType.Open,
                    Requirements = null
                };

            List<ContentKeyAuthorizationPolicyRestriction> restrictions = new List<ContentKeyAuthorizationPolicyRestriction>();
            restrictions.Add(restriction);

            var policyOption = v2Client.ContentKeyAuthorizationPolicyOptions.Create("policy", ContentKeyDeliveryType.BaselineHttp, restrictions, "");
            policy.Options.Add(policyOption);

            // Add ContentKeyAuthorizationPolicy to ContentKey
            key.AuthorizationPolicyId = policy.Id;
            key.Update();

            assetToPublish.ContentKeys.Add(key);

            Uri keyAcquisitionUri = key.GetKeyDeliveryUrl(ContentKeyDeliveryType.BaselineHttp);
            UriBuilder uriBuilder = new UriBuilder(keyAcquisitionUri);
            uriBuilder.Query = String.Empty;
            keyAcquisitionUri = uriBuilder.Uri;

            // The following policy configuration specifies: 
            //   key url that will have KID=<Guid> appended to the envelope and
            //   the Initialization Vector (IV) to use for the envelope encryption.
            var assetDeliveryPolicyConfiguration = new Dictionary<AssetDeliveryPolicyConfigurationKey, string>
            {
                {AssetDeliveryPolicyConfigurationKey.EnvelopeBaseKeyAcquisitionUrl, keyAcquisitionUri.ToString()},
            };

            var assetDeliveryPolicy = v2Client.AssetDeliveryPolicies.Create("AssetDeliveryPolicy",
                                                                            AssetDeliveryPolicyType.DynamicEnvelopeEncryption,
                                                                            AssetDeliveryProtocol.SmoothStreaming | AssetDeliveryProtocol.HLS | AssetDeliveryProtocol.Dash,
                                                                            assetDeliveryPolicyConfiguration);

            // Add AssetDelivery Policy to the asset
            assetToPublish.DeliveryPolicies.Add(assetDeliveryPolicy);

            // Create a 30-day readonly access policy. 
            // You cannot create a streaming locator using an AccessPolicy that includes write or delete permissions.
            IAccessPolicy accessPolicy = v2Client.AccessPolicies.Create("Streaming Access Policy", TimeSpan.FromDays(365*100), AccessPermissions.Read);

            // Create a locator to the streaming content on an origin. 
            ILocator originLocator = v2Client.Locators.CreateLocator(LocatorType.OnDemandOrigin, assetToPublish, accessPolicy, DateTime.UtcNow.AddMinutes(-5));

            // remove the original input asset as we don't need it for demonstration purposes
            originalAsset.Delete();

            return assetToPublish;
        }

        static AzureMediaServicesClient GetV3Client(ConfigWrapper config)
        {
            ClientCredential clientCredential = new ClientCredential(config.AadClientId, config.AadSecret);
            var credentials = ApplicationTokenProvider.LoginSilentAsync(config.AadTenantId, clientCredential, ActiveDirectoryServiceSettings.Azure).Result;

            AzureMediaServicesClient v3Client = new AzureMediaServicesClient(config.ArmEndpoint, credentials)
            {
                SubscriptionId = config.SubscriptionId,
            };

            return v3Client;
        }

        static void Main(string[] args)
        {
            string assetNameOrDescription = "assetNameOrDescription";

            string configurationFilePath = Path.Combine(Directory.GetCurrentDirectory(), "settings.json");
            ConfigWrapper config = new ConfigWrapper(configurationFilePath);

            CloudMediaContext v2Client = GetV2Client(config);
            AzureMediaServicesClient v3Client = GetV3Client(config);

            // Normally this would already exist but creating an Asset published with V2 for demonstration/testing purposes
            IAsset originalAsset = CreateAssetAndProtectedStreamingLocatorInV2(v2Client, config, assetNameOrDescription);
            // Alternatively, you can lookup an asset with a known identifier like this:
            //IAsset originalAsset = v2Client.Assets.Where(a => a.Id == "nb:cid:UUID:e9a2b9bf-aa86-48ff-baa9-ee1ed066535e").First();

            string assetNameInv3 = Getv3AssetNameFromv2Asset(originalAsset);
            Console.WriteLine("Asset {0} created in V2 is named {1} in V3", originalAsset.Id, assetNameInv3);
            Console.WriteLine();

            //
            // Show how to get the Locators, ContentKeys, and policies associated with the Asset in V2
            //
            Console.WriteLine("Locators associated with Asset {0} viewed from the V2 API:", originalAsset.Id);
            foreach (ILocator locator in v2Client.Locators.Where(l => l.AssetId == originalAsset.Id))
            {
                Console.WriteLine(locator.Id);
            }
            Console.WriteLine();

            Console.WriteLine("AssetDelivery Policies associated with the Asset viewed from the V2 API:");
            foreach (IAssetDeliveryPolicy deliveryPolicy in originalAsset.DeliveryPolicies)
            {
                Console.WriteLine(deliveryPolicy.Id);
            }
            Console.WriteLine();

            Console.WriteLine("ContentKeys associated with the Asset viewed from the V2 API:");
            foreach (IContentKey contentKey in originalAsset.ContentKeys)
            {
                Console.WriteLine("{0} with type {1} and authorization policy {2}", contentKey.Id, contentKey.ContentKeyType, contentKey.AuthorizationPolicyId);
            }

            Console.WriteLine();

            //
            // Show how to get the Locators, ContentKeys, and policies associated with the Asset in V3
            //
            // If the asset is not found it will throw an ErrorResponseException here...
            Asset v3Asset = v3Client.Assets.Get(config.ResourceGroup, config.AccountName, assetNameInv3); // really just to demonstrate we can retrieve the asset via its name

            Console.WriteLine("Locators associated with Asset {0} viewed from the V3 API:", v3Asset.Name);
            foreach (AssetStreamingLocator assetStremingLocator in v3Client.Assets.ListStreamingLocatorsAsync(config.ResourceGroup, config.AccountName, assetNameInv3).Result.StreamingLocators)
            {
                StreamingLocator locator = v3Client.StreamingLocators.Get(config.ResourceGroup, config.AccountName, assetStremingLocator.Name);

                Console.WriteLine("Locator {0} with StreamingPolicy {1}", locator.Name, locator.StreamingPolicyName);
                Console.WriteLine();

                // Note that in V3 ContentKeys are associated with the StreamingLocator and not the Asset itself.  This allows a new StreamingLocator to be created with different
                // ContentKeys if desired.  Having different keys for different urls can be used for key rotation or to use different keys in different scenarios for the same Asset.
                Console.WriteLine("ContentKeys associated with the StreamingLocator viewed from the V3 API:");
                foreach (StreamingLocatorContentKey contentKey in locator.ContentKeys)
                {
                    Console.WriteLine("{0} with type {1} and content key policy {2}", contentKey.Id, contentKey.Type, contentKey.PolicyName);
                }
                Console.WriteLine();
            }

            // If you need to change something about how an Asset is published and that Asset was originally published from V2, the recommendation is to
            // unpublish it from V2, remove the content keys and policies from V2, and then republish from V3 using V3 content keys and policies.  As an example
            // suppose you wanted to change from an open restriction (only used for testing key delivery) to a token restriction.  Here is an example of how
            // to do the unpublish and cleanup in V2 and republish in V3:
            foreach (var v2Locator in v2Client.Locators.Where(l => l.AssetId == originalAsset.Id))
            {
                v2Locator.Delete();
            }

            foreach (IAssetDeliveryPolicy deliveryPolicy in originalAsset.DeliveryPolicies.ToList())
            {
                // Unlink the policy
                originalAsset.DeliveryPolicies.Remove(deliveryPolicy);

                // Delete the policy.
                // In this example, the policy is only used by one Asset but if the policy is shared then it should be deleted when all Assets using it are unlinked.
                deliveryPolicy.Delete();
            }

            foreach (IContentKey key in originalAsset.ContentKeys.ToList())
            {
                if (key.ContentKeyType != ContentKeyType.StorageEncryption)
                {
                    if (key.AuthorizationPolicyId != null)
                    {
                        // save the id so we can delete it after unlinking
                        string contentKeyAuthorizationPolicyToDelete = key.AuthorizationPolicyId;

                        // Unlink the policy
                        key.AuthorizationPolicyId = null;
                        key.Update();

                        // Delete the policy.
                        // In this example, the policy is only used by one ContentKey but if the policy is shared then it should be deleted when all keys using it are unlinked.
                        var contentKeyAuthorizationPolicy = v2Client.ContentKeyAuthorizationPolicies.Where(p => p.Id == contentKeyAuthorizationPolicyToDelete).FirstOrDefault();

                        foreach (var policyOption in contentKeyAuthorizationPolicy.Options.ToList())
                        {
                            contentKeyAuthorizationPolicy.Options.Remove(policyOption);
                            policyOption.Delete();
                        }

                        contentKeyAuthorizationPolicy.Delete();
                    }

                    // Remove the key from the Asset
                    originalAsset.ContentKeys.Remove(key);
                }
            }

            // Publish the Asset via v3.  Note that much less code is required because we are using a built in streaming policy and allowing Media Services to generate
            // the content keys for us.  We just have to define the ContentKeyPolicy to configure the token a client needs to present in order to get the content key.
            string contentKeyPolicyName = "Shared ContentKey Policy";

            List<ContentKeyPolicyOption> options = new List<ContentKeyPolicyOption>()
            {
                new ContentKeyPolicyOption(new ContentKeyPolicyClearKeyConfiguration(), 
                                           new ContentKeyPolicyTokenRestriction("your issuer here", "your audience here", null, ContentKeyPolicyRestrictionTokenType.Jwt, openIdConnectDiscoveryDocument: "https://yourhost/yourdiscoverydoc"))
            };
            v3Client.ContentKeyPolicies.CreateOrUpdate(config.ResourceGroup, config.AccountName, contentKeyPolicyName, options);
            
            StreamingLocator streamingLocator = new StreamingLocator(assetNameInv3, PredefinedStreamingPolicy.ClearKey, defaultContentKeyPolicyName: contentKeyPolicyName);
            v3Client.StreamingLocators.Create(config.ResourceGroup, config.AccountName, "Locator for " + assetNameInv3, streamingLocator);
        }
    }
}

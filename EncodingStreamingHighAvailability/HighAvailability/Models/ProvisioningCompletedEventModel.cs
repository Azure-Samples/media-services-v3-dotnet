namespace HighAvailability.Models
{
    using Microsoft.Azure.Management.Media.Models;
    using System.Collections.Generic;

    /// <summary>
    /// Implements data model to store provisioning completed event data
    /// </summary>
    public class ProvisioningCompletedEventModel
    {
        /// <summary>
        /// Unique event id
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Encoding job output asset name
        /// </summary>
        public string AssetName { get; set; }

        /// <summary>
        /// Primary Url that can be used to stream asset data
        /// </summary>
#pragma warning disable CA1056 // Uri properties should not be strings
        public string PrimaryUrl { get; set; }
#pragma warning restore CA1056 // Uri properties should not be strings

        /// <summary>
        /// Encoded asset is provisioned to list of Azure Media Services instances. 
        /// This is the list to track these instances.
        /// </summary>
        public IList<string> MediaServiceAccountNames { get; } = new List<string>();

        /// <summary>
        /// Adds provisioned Azure Media Services instance account name
        /// </summary>
        /// <param name="mediaServiceAccountName">account name</param>
        public void AddMediaServiceAccountName(string mediaServiceAccountName)
        {
            this.MediaServiceAccountNames.Add(mediaServiceAccountName);
        }

        /// <summary>
        /// Clear streaming locators are provisioned for encoded assets.
        /// This is the list to track these locators
        /// </summary>
        public IList<StreamingLocator> ClearStreamingLocators { get; } = new List<StreamingLocator>();

        /// <summary>
        /// Adds provisioned clear streaming locator
        /// </summary>
        /// <param name="streamingLocator">locator to add</param>
        public void AddClearStreamingLocators(StreamingLocator streamingLocator)
        {
            this.ClearStreamingLocators.Add(streamingLocator);
        }

        /// <summary>
        /// Clear key streaming locators are provisioned for encoded assets.
        /// This is the list to track these locators
        public IList<StreamingLocator> ClearKeyStreamingLocators { get; } = new List<StreamingLocator>();

        /// <summary>
        /// Adds provisioned clear key streaming locator
        /// </summary>
        /// <param name="streamingLocator"></param>
        public void AddClearKeyStreamingLocators(StreamingLocator streamingLocator)
        {
            this.ClearKeyStreamingLocators.Add(streamingLocator);
        }
    }
}

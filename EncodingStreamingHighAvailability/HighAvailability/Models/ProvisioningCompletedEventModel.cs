using Microsoft.Azure.Management.Media.Models;
using System.Collections.Generic;

namespace HighAvailability.Models
{
    public class ProvisioningCompletedEventModel
    {
        public string Id { get; set; }
        public string AssetName { get; set; }
#pragma warning disable CA1056 // Uri properties should not be strings
        public string PrimaryUrl { get; set; }
#pragma warning restore CA1056 // Uri properties should not be strings

        public void AddMediaServiceAccountName(string mediaServiceAccountName)
        {
            this.MediaServiceAccountNames.Add(mediaServiceAccountName);
        }

        public IList<string> MediaServiceAccountNames { get; } = new List<string>();

        public void AddClearStreamingLocators(StreamingLocator streamingLocator)
        {
            this.ClearStreamingLocators.Add(streamingLocator);
        }

        public void AddClearKeyStreamingLocators(StreamingLocator streamingLocator)
        {
            this.ClearKeyStreamingLocators.Add(streamingLocator);
        }

        public IList<StreamingLocator> ClearStreamingLocators { get; } = new List<StreamingLocator>();

        public IList<StreamingLocator> ClearKeyStreamingLocators { get; } = new List<StreamingLocator>();
    }
}

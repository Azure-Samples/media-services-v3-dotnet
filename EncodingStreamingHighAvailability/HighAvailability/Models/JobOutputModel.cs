namespace HighAvailability.Models
{
    using Microsoft.Azure.EventGrid.Models;
    using Newtonsoft.Json;

    public class JobOutputModel : MediaJobOutput
    {
        public JobOutputModel()
        {
            this.OutputAssetName = string.Empty;
        }

        [JsonProperty(PropertyName = "assetName")]
        public string OutputAssetName { get; set; }
    }
}

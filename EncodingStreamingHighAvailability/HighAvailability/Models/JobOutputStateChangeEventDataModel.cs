namespace HighAvailability.Models
{
    using Microsoft.Azure.EventGrid.Models;
    using Newtonsoft.Json;
    public class JobOutputStateChangeEventDataModel : MediaJobOutputStateChangeEventData
    {
        public JobOutputStateChangeEventDataModel()
        {
            this.JobOutput = new JobOutputModel();
        }

        [JsonProperty(PropertyName = "output")]
        public JobOutputModel JobOutput { get; set; }
    }
}

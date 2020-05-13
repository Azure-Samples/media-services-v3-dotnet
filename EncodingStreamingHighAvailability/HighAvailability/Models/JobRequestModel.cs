namespace HighAvailability.Models
{
    using Microsoft.Azure.Management.Media.Models;

    public class JobRequestModel
    {
        public string Id { get; set; }
        public string JobName { get; set; }
        public string TransformName { get; set; }
        public JobInputs JobInputs { get; set; }
        public string OutputAssetName { get; set; }
        public string InputAssetName { get; set; }
    }
}

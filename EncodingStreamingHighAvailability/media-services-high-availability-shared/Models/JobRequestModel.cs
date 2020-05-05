namespace media_services_high_availability_shared.Models
{
    using Microsoft.Azure.Management.Media.Models;

    public class JobRequestModel
    {
        public JobRequestModel()
        {
            this.Id = string.Empty;
            this.JobName = string.Empty;
            this.TransformName = string.Empty;
            this.JobInputs = new JobInputs();
            this.OutputAssetName = string.Empty;
            this.InputAssetName = string.Empty;
        }

        public string Id { get; set; }
        public string JobName { get; set; }
        public string TransformName { get; set; }
        public JobInputs JobInputs { get; set; }
        public string OutputAssetName { get; set; }
        public string InputAssetName { get; set; }
    }
}

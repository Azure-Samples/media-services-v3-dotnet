#pragma warning disable CA1707 // Identifiers should not contain underscores
namespace media_services_high_availability_shared.Helpers
#pragma warning restore CA1707 // Identifiers should not contain underscores
{
    using Newtonsoft.Json;

    public static class LogHelper
    {
        public static string FormatObjectForLog<T>(T objectToFormat)
        {
            return JsonConvert.SerializeObject(objectToFormat);
        }
    }
}

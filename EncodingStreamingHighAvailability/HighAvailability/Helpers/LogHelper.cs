namespace HighAvailability.Helpers
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

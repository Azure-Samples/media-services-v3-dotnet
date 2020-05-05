namespace media_services_high_availability_shared.Helpers
{
    using System;
    using System.Text;

    public static class QueueServiceHelper
    {
        public static string EncodeToBase64(string message)
        {
            var encoding = new UTF8Encoding();
            var encodedBytes = encoding.GetBytes(message);
            return Convert.ToBase64String(encodedBytes);
        }

        public static string DecodeFromBase64(string message)
        {
            var utf8Bytes = Convert.FromBase64String(message);
            var encoding = new UTF8Encoding();
            return encoding.GetString(utf8Bytes);
        }
    }
}

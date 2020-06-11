namespace HighAvailability.Helpers
{
    using System;
    using System.Text;

    /// <summary>
    /// Implements helper methods for Azure Queue service
    /// </summary>
    public static class QueueServiceHelper
    {
        /// <summary>
        /// Encodes string to base64 encoding, all messages should be encoded before sending to Azure Queue
        /// </summary>
        /// <param name="message">string to encode</param>
        /// <returns>encoded string</returns>
        public static string EncodeToBase64(string message)
        {
            var encoding = new UTF8Encoding();
            var encodedBytes = encoding.GetBytes(message);
            return Convert.ToBase64String(encodedBytes);
        }

        /// <summary>
        /// Decodes string from base64 encoding, all messages in Azure Queue are base64 encoded
        /// </summary>
        /// <param name="message">message to decode</param>
        /// <returns>decoded message</returns>
        public static string DecodeFromBase64(string message)
        {
            var utf8Bytes = Convert.FromBase64String(message);
            var encoding = new UTF8Encoding();
            return encoding.GetString(utf8Bytes);
        }
    }
}

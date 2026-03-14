using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Egroo.UI.Helpers
{
    public static class EncryptedFileTokenParser
    {
        public static EncryptedFileDescriptor Parse(string token)
        {
            try
            {
                string json = Encoding.UTF8.GetString(Base64UrlDecode(token));
                var metadata = JsonSerializer.Deserialize<EncryptedFileTokenMetadata>(json);
                string label = string.IsNullOrWhiteSpace(metadata?.Name)
                    ? "Encrypted file"
                    : metadata.Name;

                return new EncryptedFileDescriptor(label, IsImageContentType(metadata?.MediaType));
            }
            catch
            {
                return new EncryptedFileDescriptor("Encrypted file", false);
            }
        }

        private static bool IsImageContentType(string? contentType)
        {
            return !string.IsNullOrWhiteSpace(contentType)
                && contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
        }

        private static byte[] Base64UrlDecode(string value)
        {
            string padded = value.Replace('-', '+').Replace('_', '/');
            int remainder = padded.Length % 4;
            if (remainder > 0)
            {
                padded = padded.PadRight(padded.Length + (4 - remainder), '=');
            }

            return Convert.FromBase64String(padded);
        }

        private sealed record EncryptedFileTokenMetadata(
            [property: JsonPropertyName("FileName")] string? Name,
            [property: JsonPropertyName("ContentType")] string? MediaType);

        public sealed record EncryptedFileDescriptor(string Label, bool IsImage);
    }
}
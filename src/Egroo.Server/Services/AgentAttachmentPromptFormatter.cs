using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Egroo.Server.Services
{
    internal static class AgentAttachmentPromptFormatter
    {
        private static readonly Regex EncryptedFileRegex = new(
            @"\[\[egroo-file:(?<token>[A-Za-z0-9_-]+)\]\]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex MarkdownImageDataUrlRegex = new(
            @"!\[(?<alt>[^\]]*)\]\((?<url>data:[^)]+)\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex MarkdownLinkDataUrlRegex = new(
            @"\[(?<label>[^\]]+)\]\((?<url>data:[^)]+)\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static string Normalize(string? content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return string.Empty;
            }

            string normalized = EncryptedFileRegex.Replace(content, ReplaceEncryptedFileToken);
            normalized = MarkdownImageDataUrlRegex.Replace(normalized, match =>
            {
                string label = string.IsNullOrWhiteSpace(match.Groups["alt"].Value)
                    ? "Image"
                    : match.Groups["alt"].Value.Trim();

                return $"[Attached image: {label}]";
            });

            normalized = MarkdownLinkDataUrlRegex.Replace(normalized, match =>
            {
                string label = string.IsNullOrWhiteSpace(match.Groups["label"].Value)
                    ? "File"
                    : match.Groups["label"].Value.Trim();

                return $"[Attached file: {label}]";
            });

            return normalized;
        }

        private static string ReplaceEncryptedFileToken(Match match)
        {
            string token = match.Groups["token"].Value;

            try
            {
                string json = Encoding.UTF8.GetString(Base64UrlDecode(token));
                using JsonDocument document = JsonDocument.Parse(json);
                JsonElement root = document.RootElement;

                string label = GetString(root, "FileName")
                    ?? GetString(root, "fileName")
                    ?? "Encrypted file";

                string? contentType = GetString(root, "ContentType")
                    ?? GetString(root, "contentType");

                return IsImageContentType(contentType)
                    ? $"[Attached image: {label}]"
                    : $"[Attached file: {label}]";
            }
            catch
            {
                return "[Attached file: Encrypted file]";
            }
        }

        private static string? GetString(JsonElement root, string propertyName)
        {
            return root.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
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
    }
}
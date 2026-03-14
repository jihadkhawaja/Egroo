using System.Net;
using System.Text.RegularExpressions;
using jihadkhawaja.chat.shared.Models;
using Microsoft.Extensions.AI;

namespace Egroo.Server.Services
{
    internal static class AgentChatMessageFactory
    {
        private static readonly Regex MarkdownImageDataUrlRegex = new(
            @"!\[(?<alt>[^\]]*)\]\((?<url>data:(?<mediaType>[^;\)]+)[^)]+)\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static ChatMessage CreateUserMessage(AgentChatRequest request)
        {
            var contents = new List<AIContent>();

            AddTextContent(contents, request.Message);

            foreach (var attachment in request.Attachments ?? Array.Empty<AgentChatAttachment>())
            {
                if (string.IsNullOrWhiteSpace(attachment.DataUri))
                {
                    AddTextContent(contents, $"[Attached file: {attachment.FileName}]");
                    continue;
                }

                string mediaType = ResolveMediaType(attachment.ContentType, attachment.DataUri);
                if (IsImageContentType(mediaType))
                {
                    contents.Add(new DataContent(attachment.DataUri, mediaType));
                }
                else
                {
                    AddTextContent(contents, $"[Attached file: {attachment.FileName}]");
                }
            }

            return CreateChatMessage(ChatRole.User, contents);
        }

        public static ChatMessage CreateStoredMessage(ChatRole role, string? content)
        {
            if (role != ChatRole.User)
            {
                return new ChatMessage(role, content ?? string.Empty);
            }

            var contents = new List<AIContent>();
            string source = content ?? string.Empty;
            int cursor = 0;

            foreach (Match match in MarkdownImageDataUrlRegex.Matches(source))
            {
                string before = source[cursor..match.Index];
                AddTextContent(contents, before);

                string dataUri = WebUtility.HtmlDecode(match.Groups["url"].Value);
                string mediaType = ResolveMediaType(match.Groups["mediaType"].Value, dataUri);
                if (IsImageContentType(mediaType))
                {
                    contents.Add(new DataContent(dataUri, mediaType));
                }

                cursor = match.Index + match.Length;
            }

            if (cursor < source.Length)
            {
                AddTextContent(contents, source[cursor..]);
            }

            return CreateChatMessage(role, contents);
        }

        private static ChatMessage CreateChatMessage(ChatRole role, IList<AIContent> contents)
        {
            if (contents.Count == 1 && contents[0] is TextContent text)
            {
                return new ChatMessage(role, text.Text);
            }

            if (contents.Count == 0)
            {
                return new ChatMessage(role, string.Empty);
            }

            return new ChatMessage(role, contents);
        }

        private static void AddTextContent(ICollection<AIContent> contents, string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            string normalized = AgentAttachmentPromptFormatter.Normalize(text)
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Trim();

            if (string.IsNullOrWhiteSpace(normalized))
            {
                return;
            }

            contents.Add(new TextContent(normalized));
        }

        private static string ResolveMediaType(string? contentType, string dataUri)
        {
            if (!string.IsNullOrWhiteSpace(contentType))
            {
                return contentType;
            }

            const string prefix = "data:";
            if (dataUri.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                int separatorIndex = dataUri.IndexOf(';', prefix.Length);
                if (separatorIndex > prefix.Length)
                {
                    return dataUri[prefix.Length..separatorIndex];
                }
            }

            return "application/octet-stream";
        }

        private static bool IsImageContentType(string? contentType)
        {
            return !string.IsNullOrWhiteSpace(contentType)
                && contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
        }
    }
}

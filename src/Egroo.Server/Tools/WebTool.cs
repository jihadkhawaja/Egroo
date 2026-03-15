using Microsoft.Extensions.AI;
using System.ComponentModel;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;

namespace Egroo.Server.Services
{
    internal static class WebTool
    {
        private static readonly HttpClient WebHttpClient = CreateWebHttpClient();
        private static readonly Regex AnchorRegex = new("""<a\b[^>]*href\s*=\s*['"](?<href>[^'"]+)['"][^>]*>(?<text>.*?)</a>""", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex TitleRegex = new(@"<title[^>]*>(?<title>.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex StripTagRegex = new(@"<[^>]+>", RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex MultiWhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
        private const int DefaultWebSearchResultCount = 5;
        private const int MaxWebSearchResultCount = 10;
        private const int DefaultWebPageCharacterLimit = 4000;
        private const int MaxWebPageCharacterLimit = 12000;

        public static IReadOnlyList<BuiltinToolDefinition> GetDefinitions() =>
        [
            new()
            {
                Name = "search_web",
                Description = "Search the public web using a free HTML search endpoint and return titles, URLs, and snippets from matching pages. Use this when the user asks for current web information or external sources.",
                ParametersSchema = """{"type":"object","properties":{"query":{"type":"string","description":"The search query to run against the public web"},"max_results":{"type":"integer","description":"Maximum number of search results to return (default: 5, max: 10)"}},"required":["query"]}"""
            },
            new()
            {
                Name = "fetch_web_page",
                Description = "Fetch a public HTTP or HTTPS webpage and extract readable text content. Use this after search_web when the user wants details from a specific page.",
                ParametersSchema = """{"type":"object","properties":{"url":{"type":"string","description":"The public HTTP or HTTPS URL to fetch"},"max_chars":{"type":"integer","description":"Maximum number of readable characters to return from the page (default: 4000, max: 12000)"}},"required":["url"]}"""
            }
        ];

        public static IList<AITool> CreateTools()
        {
            return new List<AITool>
            {
                AIFunctionFactory.Create(SearchWeb, "search_web",
                    "Search the public web and return matching titles, URLs, and snippets."),
                AIFunctionFactory.Create(FetchWebPage, "fetch_web_page",
                    "Fetch a public webpage and extract readable text content.")
            };
        }

        [Description("Search the public web and return matching titles, URLs, and snippets.")]
        private static async Task<string> SearchWeb(
            [Description("The search query to run against the public web")]
            string query,
            [Description("Maximum number of search results to return (default: 5, max: 10)")]
            int? max_results = null)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return "Search query is required.";
            }

            var maxResults = Math.Clamp(max_results ?? DefaultWebSearchResultCount, 1, MaxWebSearchResultCount);
            var searchUrl = new Uri($"https://html.duckduckgo.com/html/?q={Uri.EscapeDataString(query)}");
            var response = await GetWebResponseAsync(searchUrl);
            if (response.Error is not null)
            {
                return response.Error;
            }

            var results = ParseDuckDuckGoResults(response.Content ?? string.Empty, maxResults);
            if (results.Count == 0)
            {
                return $"No public web results were found for '{query}'.";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Web results for '{query}':");

            for (var index = 0; index < results.Count; index++)
            {
                var result = results[index];
                sb.AppendLine($"{index + 1}. {result.Title}");
                sb.AppendLine($"   URL: {result.Url}");

                if (!string.IsNullOrWhiteSpace(result.Snippet))
                {
                    sb.AppendLine($"   Snippet: {result.Snippet}");
                }
            }

            sb.AppendLine();
            sb.AppendLine("Source: DuckDuckGo HTML search.");
            return sb.ToString();
        }

        [Description("Fetch a public webpage and extract readable text content.")]
        private static async Task<string> FetchWebPage(
            [Description("The public HTTP or HTTPS URL to fetch")]
            string url,
            [Description("Maximum number of readable characters to return from the page (default: 4000, max: 12000)")]
            int? max_chars = null)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return "Invalid URL. Provide an absolute HTTP or HTTPS URL.";
            }

            var charLimit = Math.Clamp(max_chars ?? DefaultWebPageCharacterLimit, 500, MaxWebPageCharacterLimit);
            var response = await GetWebResponseAsync(uri);
            if (response.Error is not null)
            {
                return response.Error;
            }

            var contentType = response.ContentType ?? string.Empty;
            if (!contentType.Contains("html", StringComparison.OrdinalIgnoreCase)
                && !contentType.Contains("text/plain", StringComparison.OrdinalIgnoreCase)
                && !contentType.Contains("xml", StringComparison.OrdinalIgnoreCase))
            {
                return $"Unsupported content type '{contentType}'. Only text-based pages are supported.";
            }

            var extracted = ExtractReadableContentFromHtml(response.Content ?? string.Empty, charLimit);
            if (string.IsNullOrWhiteSpace(extracted.Content))
            {
                return "The page was fetched, but no readable text content could be extracted.";
            }

            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(extracted.Title))
            {
                sb.AppendLine($"Title: {extracted.Title}");
            }

            sb.AppendLine($"URL: {response.FinalUri}");
            sb.AppendLine();
            sb.AppendLine(extracted.Content);

            if (extracted.WasTruncated)
            {
                sb.AppendLine();
                sb.AppendLine("[Content truncated to fit tool output.]");
            }

            return sb.ToString().TrimEnd();
        }

        internal static IReadOnlyList<WebSearchResult> ParseDuckDuckGoResults(string html, int maxResults)
        {
            if (string.IsNullOrWhiteSpace(html) || maxResults <= 0)
            {
                return [];
            }

            var results = new List<WebSearchResult>(maxResults);
            var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (Match match in AnchorRegex.Matches(html))
            {
                var title = NormalizeWhitespace(WebUtility.HtmlDecode(StripHtml(match.Groups["text"].Value)));
                if (string.IsNullOrWhiteSpace(title))
                {
                    continue;
                }

                var normalizedUrl = NormalizeSearchResultUrl(WebUtility.HtmlDecode(match.Groups["href"].Value));
                if (normalizedUrl is null || !seenUrls.Add(normalizedUrl))
                {
                    continue;
                }

                var snippet = ExtractNearbySnippet(html, match.Index + match.Length, title);
                results.Add(new WebSearchResult(title, normalizedUrl, snippet));

                if (results.Count >= maxResults)
                {
                    break;
                }
            }

            return results;
        }

        internal static WebPageContent ExtractReadableContentFromHtml(string html, int maxChars)
        {
            var titleMatch = TitleRegex.Match(html);
            var title = titleMatch.Success
                ? NormalizeWhitespace(WebUtility.HtmlDecode(StripHtml(titleMatch.Groups["title"].Value)))
                : null;

            var cleaned = Regex.Replace(html, @"<(script|style|noscript|svg|iframe|canvas)[^>]*>.*?</\1>", " ", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            cleaned = Regex.Replace(cleaned, @"</?(p|div|section|article|main|aside|header|footer|nav|li|ul|ol|table|tr|td|th|h1|h2|h3|h4|h5|h6|blockquote|pre|br)[^>]*>", "\n", RegexOptions.IgnoreCase);
            cleaned = WebUtility.HtmlDecode(StripHtml(cleaned));

            var lines = cleaned
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(NormalizeWhitespace)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();

            var builder = new StringBuilder();
            var wasTruncated = false;

            foreach (var line in lines)
            {
                AppendWithLimit(builder, builder.Length == 0 ? line : Environment.NewLine + line, maxChars, ref wasTruncated);
                if (wasTruncated)
                {
                    break;
                }
            }

            return new WebPageContent(title, builder.ToString().Trim(), wasTruncated);
        }

        internal static async Task<string?> ValidatePublicInternetUrlAsync(Uri uri)
        {
            if (!uri.IsAbsoluteUri)
            {
                return "Invalid URL. Provide an absolute HTTP or HTTPS URL.";
            }

            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return "Only public HTTP or HTTPS URLs are allowed.";
            }

            if (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
                || uri.Host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
                || uri.Host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase))
            {
                return "Only public internet URLs are allowed.";
            }

            if (IPAddress.TryParse(uri.Host, out var directIp))
            {
                return IsPublicIpAddress(directIp) ? null : "Only public internet URLs are allowed.";
            }

            IPAddress[] resolvedAddresses;
            try
            {
                resolvedAddresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost);
            }
            catch
            {
                return "Unable to resolve the requested hostname.";
            }

            if (resolvedAddresses.Length == 0 || resolvedAddresses.Any(address => !IsPublicIpAddress(address)))
            {
                return "Only public internet URLs are allowed.";
            }

            return null;
        }

        private static HttpClient CreateWebHttpClient()
        {
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
            };

            var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(15)
            };

            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "EgrooBot/1.0 (+https://github.com/jihadkhawaja/Egroo)");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,text/plain;q=0.9,*/*;q=0.1");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.8");
            return client;
        }

        private static async Task<WebResponseResult> GetWebResponseAsync(Uri initialUri)
        {
            Uri currentUri = initialUri;

            for (var redirectCount = 0; redirectCount < 5; redirectCount++)
            {
                var validationError = await ValidatePublicInternetUrlAsync(currentUri);
                if (validationError is not null)
                {
                    return new WebResponseResult(null, currentUri, null, validationError);
                }

                using var request = new HttpRequestMessage(HttpMethod.Get, currentUri);
                using var response = await WebHttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                if (IsRedirectStatusCode(response.StatusCode))
                {
                    if (response.Headers.Location is null)
                    {
                        return new WebResponseResult(null, currentUri, null, "The website returned a redirect without a location.");
                    }

                    currentUri = response.Headers.Location.IsAbsoluteUri
                        ? response.Headers.Location
                        : new Uri(currentUri, response.Headers.Location);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    return new WebResponseResult(null, currentUri, null, $"The website returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).");
                }

                return new WebResponseResult(await response.Content.ReadAsStringAsync(), currentUri, response.Content.Headers.ContentType?.MediaType, null);
            }

            return new WebResponseResult(null, currentUri, null, "The website redirected too many times.");
        }

        private static bool IsRedirectStatusCode(HttpStatusCode statusCode)
        {
            var numeric = (int)statusCode;
            return numeric >= 300 && numeric < 400;
        }

        private static string? NormalizeSearchResultUrl(string href)
        {
            if (string.IsNullOrWhiteSpace(href))
            {
                return null;
            }

            if (href.StartsWith("//", StringComparison.Ordinal))
            {
                href = $"https:{href}";
            }

            if (!Uri.TryCreate(href, UriKind.Absolute, out var uri)
                && !Uri.TryCreate(new Uri("https://duckduckgo.com"), href, out uri))
            {
                return null;
            }

            if (uri.Host.Contains("duckduckgo.com", StringComparison.OrdinalIgnoreCase))
            {
                var redirectedUrl = TryGetQueryParameter(uri.Query, "uddg");
                if (string.IsNullOrWhiteSpace(redirectedUrl)
                    || !Uri.TryCreate(Uri.UnescapeDataString(redirectedUrl), UriKind.Absolute, out uri))
                {
                    return null;
                }
            }

            return string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                ? uri.ToString()
                : null;
        }

        private static string? TryGetQueryParameter(string query, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return null;
            }

            foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = pair.Split('=', 2);
                if (parts.Length == 2 && string.Equals(parts[0], parameterName, StringComparison.OrdinalIgnoreCase))
                {
                    return parts[1];
                }
            }

            return null;
        }

        private static string ExtractNearbySnippet(string html, int startIndex, string title)
        {
            if (startIndex >= html.Length)
            {
                return string.Empty;
            }

            var maxLength = Math.Min(800, html.Length - startIndex);
            var rawSlice = html.Substring(startIndex, maxLength);
            rawSlice = Regex.Replace(rawSlice, @"<a\b[^>]*>.*?</a>", " ", RegexOptions.IgnoreCase | RegexOptions.Singleline);

            var snippet = NormalizeWhitespace(WebUtility.HtmlDecode(StripHtml(rawSlice)));
            if (string.IsNullOrWhiteSpace(snippet))
            {
                return string.Empty;
            }

            if (snippet.StartsWith(title, StringComparison.OrdinalIgnoreCase))
            {
                snippet = snippet[title.Length..].TrimStart(' ', '-', ':');
            }

            return snippet.Length > 240 ? snippet[..240].TrimEnd() + "..." : snippet;
        }

        private static string StripHtml(string value) => StripTagRegex.Replace(value, " ");

        private static string NormalizeWhitespace(string value) => MultiWhitespaceRegex.Replace(value, " ").Trim();

        private static void AppendWithLimit(StringBuilder builder, string text, int maxChars, ref bool wasTruncated)
        {
            if (builder.Length + text.Length <= maxChars)
            {
                builder.Append(text);
                return;
            }

            var remaining = maxChars - builder.Length;
            if (remaining > 0)
            {
                builder.Append(text[..remaining]);
            }

            wasTruncated = true;
        }

        private static bool IsPublicIpAddress(IPAddress address)
        {
            if (IPAddress.IsLoopback(address))
            {
                return false;
            }

            if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
            {
                if (address.IsIPv6LinkLocal || address.IsIPv6Multicast || address.IsIPv6SiteLocal)
                {
                    return false;
                }

                var bytes = address.GetAddressBytes();
                return (bytes[0] & 0xFE) != 0xFC;
            }

            var ipv4 = address.MapToIPv4().GetAddressBytes();
            return !(ipv4[0] == 10
                || ipv4[0] == 127
                || (ipv4[0] == 169 && ipv4[1] == 254)
                || (ipv4[0] == 172 && ipv4[1] >= 16 && ipv4[1] <= 31)
                || (ipv4[0] == 192 && ipv4[1] == 168)
                || (ipv4[0] == 100 && ipv4[1] >= 64 && ipv4[1] <= 127)
                || ipv4[0] == 0);
        }
    }

    internal sealed record WebSearchResult(string Title, string Url, string Snippet);

    internal sealed record WebPageContent(string? Title, string Content, bool WasTruncated);

    internal sealed record WebResponseResult(string? Content, Uri FinalUri, string? ContentType, string? Error);
}
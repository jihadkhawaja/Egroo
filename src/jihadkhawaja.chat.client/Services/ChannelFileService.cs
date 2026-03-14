using jihadkhawaja.chat.shared.Models;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace jihadkhawaja.chat.client.Services
{
    public class ChannelFileService
    {
        private const string BasePath = "api/v1/ChannelFiles";
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public HttpClient HttpClient { get; }

        public ChannelFileService(HttpClient http)
        {
            HttpClient = http;
        }

        public async Task<ChannelFileLink?> Upload(
            Guid channelId,
            Stream stream,
            string fileName,
            string? contentType,
            CancellationToken cancellationToken = default)
        {
            using var content = new MultipartFormDataContent();
            using var fileContent = new StreamContent(stream);

            if (!string.IsNullOrWhiteSpace(contentType))
            {
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            }

            content.Add(fileContent, "file", fileName);

            using var response = await HttpClient.PostAsync($"{BasePath}/{channelId}", content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<ChannelFileLink>(JsonOptions, cancellationToken);
        }

        public string GetAbsoluteUrl(string url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var absoluteUri))
            {
                return absoluteUri.ToString();
            }

            if (HttpClient.BaseAddress is null)
            {
                return url;
            }

            return new Uri(HttpClient.BaseAddress, url).ToString();
        }
    }
}
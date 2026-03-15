using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Egroo.Server.API
{
    public static class VoiceCallEndpoint
    {
        private static readonly string[] CloudflareTurnUrls =
        [
            "stun:stun.cloudflare.com:3478",
            "turn:turn.cloudflare.com:3478?transport=udp",
            "turn:turn.cloudflare.com:3478?transport=tcp",
            "turns:turn.cloudflare.com:5349?transport=tcp"
        ];

        private static readonly VoiceCallConfigurationResponse DefaultConfiguration = new()
        {
            IceServers =
            [
                new VoiceCallIceServerResponse
                {
                    Urls = ["stun:stun.cloudflare.com:3478"]
                }
            ]
        };

        public static void MapVoiceCall(this IEndpointRouteBuilder routes)
        {
            var group = routes.MapGroup("/api/v1/Voice")
                .WithTags("Voice")
                .RequireRateLimiting("Api");

            group.MapGet("/config", async (IConfiguration configuration, IHttpClientFactory httpClientFactory) =>
            {
                var result = await BuildConfigurationAsync(configuration, httpClientFactory);
                return Results.Ok(result);
            }).AllowAnonymous();
        }

        private static async Task<VoiceCallConfigurationResponse> BuildConfigurationAsync(
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory)
        {
            // Try to generate Cloudflare TURN credentials
            var cloudflareServer = await GenerateCloudflareCredentialsAsync(configuration, httpClientFactory);
            if (cloudflareServer is not null)
            {
                return new VoiceCallConfigurationResponse
                {
                    IceServers = [cloudflareServer]
                };
            }

            // Fall back to static config
            var configuredServers = configuration
                .GetSection("VoiceCall:IceServers")
                .Get<List<VoiceCallIceServerResponse>>();

            var normalizedServers = NormalizeServers(configuredServers);
            if (normalizedServers.Count == 0)
            {
                normalizedServers = NormalizeServers(DefaultConfiguration.IceServers);
            }

            return new VoiceCallConfigurationResponse
            {
                IceServers = normalizedServers
            };
        }

        private static async Task<VoiceCallIceServerResponse?> GenerateCloudflareCredentialsAsync(
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory)
        {
            var tokenId = configuration["VoiceCall:CloudflareTurn:TokenId"];
            var apiToken = configuration["VoiceCall:CloudflareTurn:ApiToken"];

            if (string.IsNullOrWhiteSpace(tokenId) || string.IsNullOrWhiteSpace(apiToken))
            {
                return null;
            }

            try
            {
                using var client = httpClientFactory.CreateClient();
                var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    $"https://rtc.live.cloudflare.com/v1/turn/keys/{tokenId}/credentials/generate");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
                request.Content = JsonContent.Create(new { ttl = 86400 });

                using var response = await client.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<CloudflareTurnResponse>(
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (result?.IceServers is { Username: { Length: > 0 }, Credential: { Length: > 0 } })
                {
                    return new VoiceCallIceServerResponse
                    {
                        Urls = result.IceServers.Urls?.Length > 0 ? result.IceServers.Urls : CloudflareTurnUrls,
                        Username = result.IceServers.Username,
                        Credential = result.IceServers.Credential
                    };
                }
            }
            catch
            {
                // Cloudflare credential generation failed; fall through to static config
            }

            return null;
        }

        private static List<VoiceCallIceServerResponse> NormalizeServers(IEnumerable<VoiceCallIceServerResponse>? configuredServers)
        {
            if (configuredServers is null)
            {
                return [];
            }

            var normalizedServers = new List<VoiceCallIceServerResponse>();
            foreach (var server in configuredServers)
            {
                var urls = (server.Urls ?? [])
                    .Where(url => !string.IsNullOrWhiteSpace(url))
                    .Select(url => url.Trim())
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();

                if (urls.Length == 0)
                {
                    continue;
                }

                normalizedServers.Add(new VoiceCallIceServerResponse
                {
                    Urls = urls,
                    Username = string.IsNullOrWhiteSpace(server.Username) ? null : server.Username.Trim(),
                    Credential = string.IsNullOrWhiteSpace(server.Credential) ? null : server.Credential.Trim()
                });
            }

            return normalizedServers;
        }
    }

    public sealed class VoiceCallConfigurationResponse
    {
        public List<VoiceCallIceServerResponse> IceServers { get; set; } = [];
    }

    public sealed class VoiceCallIceServerResponse
    {
        public string[] Urls { get; set; } = [];
        public string? Username { get; set; }
        public string? Credential { get; set; }
    }

    internal sealed class CloudflareTurnResponse
    {
        [JsonPropertyName("iceServers")]
        public CloudflareTurnIceServers? IceServers { get; set; }
    }

    internal sealed class CloudflareTurnIceServers
    {
        [JsonPropertyName("urls")]
        public string[]? Urls { get; set; }

        [JsonPropertyName("username")]
        public string? Username { get; set; }

        [JsonPropertyName("credential")]
        public string? Credential { get; set; }
    }
}
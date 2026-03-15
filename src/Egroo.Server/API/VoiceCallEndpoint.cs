using Microsoft.Extensions.Configuration;

namespace Egroo.Server.API
{
    public static class VoiceCallEndpoint
    {
        private static readonly VoiceCallConfigurationResponse DefaultConfiguration = new()
        {
            IceServers =
            [
                new VoiceCallIceServerResponse
                {
                    Urls =
                    [
                        "stun:stun.l.google.com:19302",
                        "stun:stun1.l.google.com:19302"
                    ]
                }
            ]
        };

        public static void MapVoiceCall(this IEndpointRouteBuilder routes)
        {
            var group = routes.MapGroup("/api/v1/Voice")
                .WithTags("Voice")
                .RequireRateLimiting("Api");

            group.MapGet("/config", (IConfiguration configuration) =>
            {
                return Results.Ok(BuildConfiguration(configuration));
            }).AllowAnonymous();
        }

        private static VoiceCallConfigurationResponse BuildConfiguration(IConfiguration configuration)
        {
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
}
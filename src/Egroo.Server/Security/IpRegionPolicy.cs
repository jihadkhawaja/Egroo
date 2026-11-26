using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using IPNetwork = System.Net.IPNetwork;

namespace Egroo.Server.Security;

public sealed class IpRegionPolicy
{
    internal const string SectionName = "IpRegionProtection";

    private readonly (IPNetwork Network, string Region)[] _networks;
    private readonly HashSet<IPAddress> _trustedProxies;

    private IpRegionPolicy(bool enabled, (IPNetwork Network, string Region)[] networks, HashSet<IPAddress> trustedProxies,
        string? configurationError = null)
    {
        Enabled = enabled;
        _networks = networks;
        _trustedProxies = trustedProxies;
        ConfigurationError = configurationError;
        Regions = Array.AsReadOnly(networks.Select(entry => entry.Region).Distinct().Order().ToArray());
    }

    internal static IpRegionPolicy Disabled { get; } = new(false, [], []);
    internal bool Enabled { get; }
    internal bool Available => Enabled && ConfigurationError is null;
    internal string? ConfigurationError { get; }
    internal IReadOnlyList<string> Regions { get; }

    internal static IpRegionPolicy FromConfiguration(IConfiguration configuration)
    {
        var settings = configuration.GetSection(SectionName);
        if (settings["Enabled"] is not { } enabledSetting)
            return Disabled;
        if (!bool.TryParse(enabledSetting, out bool enabled))
            return Unavailable("Enabled must be true or false.");
        if (!enabled)
            return Disabled;

        // Automatic hosting configuration trusts forwarding without the exact peer list below.
        if (IsEnabled(configuration["FORWARDEDHEADERS_ENABLED"])
            || IsEnabled(configuration["ASPNETCORE_FORWARDEDHEADERS_ENABLED"]))
        {
            return Unavailable("Turn off automatic forwarded headers before enabling IP-region protection.");
        }

        var proxies = new HashSet<IPAddress>();
        foreach (var entry in settings.GetSection("TrustedProxies").GetChildren())
        {
            if (!IPAddress.TryParse(entry.Value, out var address) || Normalize(address).Equals(IPAddress.Any)
                || address.Equals(IPAddress.IPv6Any))
            {
                return Unavailable("TrustedProxies must contain exact IP addresses.");
            }

            proxies.Add(Normalize(address));
        }

        var networks = new Dictionary<IPNetwork, string>();
        foreach (var region in settings.GetSection("Regions").GetChildren())
        {
            var code = NormalizeRegion(region.Key);
            if (code is null)
                return Unavailable("Region names must use two ASCII letters.");

            foreach (var entry in region.GetChildren())
            {
                if (!IPNetwork.TryParse(entry.Value, out var network) || network.BaseAddress.IsIPv4MappedToIPv6
                    || network.PrefixLength == 0)
                {
                    return Unavailable("Each region requires valid IPv4 or IPv6 CIDR ranges with a nonzero prefix.");
                }

                if (!networks.TryAdd(network, code) && !string.Equals(networks[network], code, StringComparison.Ordinal))
                    return Unavailable("The same network prefix cannot belong to different regions.");
            }
        }

        if (networks.Count == 0)
            return Unavailable("Configure at least one CIDR range for IP-region protection.");

        return new IpRegionPolicy(true, networks.OrderByDescending(pair => pair.Key.PrefixLength)
            .Select(pair => (pair.Key, pair.Value)).ToArray(), proxies);
    }

    internal string? Resolve(IPAddress? peer)
    {
        if (!Available || peer is null)
            return null;

        var address = Normalize(peer);
        if (_trustedProxies.Contains(address))
            return null;

        foreach (var (network, region) in _networks)
        {
            if (network.Contains(address))
                return region;
        }

        return null;
    }

    internal bool RequiresRecovery(string? storedRegions, IPAddress? peer)
    {
        if (!Enabled || string.IsNullOrEmpty(storedRegions))
            return false;

        var region = Resolve(peer);
        return region is null || !ReadRegions(storedRegions).Contains(region, StringComparer.Ordinal);
    }

    internal string[]? ValidateSelection(IEnumerable<string?> selection, IPAddress? peer)
    {
        var codes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var requested in selection)
        {
            var code = NormalizeRegion(requested);
            if (code is null || !Regions.Contains(code, StringComparer.Ordinal))
                return null;

            codes.Add(code);
        }

        if (codes.Count == 0)
            return [];

        return Resolve(peer) is { } current && codes.Contains(current) ? codes.Order().ToArray() : null;
    }

    internal bool IsTrustedProxy(IPAddress? peer) =>
        peer is not null && _trustedProxies.Contains(Normalize(peer));

    internal ForwardedHeadersOptions? CreateForwardedHeadersOptions()
    {
        if (!Available || _trustedProxies.Count == 0)
            return null;

        var options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            ForwardLimit = 1
        };
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();
        foreach (var address in _trustedProxies)
        {
            options.KnownProxies.Add(address);
        }

        return options;
    }

    internal static string[] ReadRegions(string? storedRegions) =>
        storedRegions?.Split('\n', StringSplitOptions.RemoveEmptyEntries) ?? [];

    private static IpRegionPolicy Unavailable(string configurationError) =>
        new(true, [], [], configurationError);

    private static bool IsEnabled(string? value) => bool.TryParse(value, out bool enabled) && enabled;

    private static IPAddress Normalize(IPAddress address) =>
        address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

    private static string? NormalizeRegion(string? value)
    {
        var trimmed = value?.Trim();
        return trimmed is { Length: 2 } && char.IsAsciiLetter(trimmed[0]) && char.IsAsciiLetter(trimmed[1])
            ? trimmed.ToUpperInvariant() : null;
    }
}

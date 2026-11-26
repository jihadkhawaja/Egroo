using System.Net;
using Egroo.Server.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Egroo.Server.Test;

[TestClass]
public class IpRegionPolicyTest
{
    [TestMethod]
    public void ResolvesTheMostSpecificIpv4AndIpv6Ranges()
    {
        var policy = Create();
        Assert.IsTrue(policy.Available);
        Assert.AreEqual("HM", policy.Resolve(IPAddress.Parse("198.51.100.42")));
        Assert.AreEqual("WK", policy.Resolve(IPAddress.Parse("::ffff:198.51.100.130")));
        Assert.AreEqual("WK", policy.Resolve(IPAddress.Parse("2001:db8:42::5")));
        Assert.IsNull(policy.Resolve(IPAddress.Parse("192.0.2.11")));
        Assert.IsNull(policy.Resolve(IPAddress.Parse("203.0.113.10")));
        Assert.IsNull(policy.Resolve(null));
    }

    [TestMethod]
    public void OnlyOptedInAccountsRequireRecoveryForUnknownOrDisallowedRegions()
    {
        var policy = Create();
        var home = IPAddress.Parse("198.51.100.42");
        var elsewhere = IPAddress.Parse("198.51.100.130");
        Assert.IsFalse(policy.RequiresRecovery(null, elsewhere));
        Assert.IsFalse(IpRegionPolicy.Disabled.RequiresRecovery("HM", elsewhere));
        Assert.IsFalse(policy.RequiresRecovery("HM", home));
        Assert.IsTrue(policy.RequiresRecovery("HM", elsewhere));
        Assert.IsTrue(policy.RequiresRecovery("HM", null));
        Assert.IsFalse(policy.RequiresRecovery("HM\nWK", elsewhere));
    }

    [TestMethod]
    public void RequiresARecognizedCurrentRegionForEnrollmentAndAllowsClearingAnywhere()
    {
        var policy = Create();
        var home = IPAddress.Parse("198.51.100.42");
        CollectionAssert.AreEqual(new[] { "HM", "WK" }, policy.ValidateSelection(["wk", " hm ", "HM"], home));
        Assert.IsNull(policy.ValidateSelection(["WK"], home));
        Assert.IsNull(policy.ValidateSelection(["HM", "ZZ"], home));
        Assert.IsNull(policy.ValidateSelection(["HM"], null));
        CollectionAssert.AreEqual(Array.Empty<string>(), policy.ValidateSelection([], null));
    }

    [TestMethod]
    [DataRow("IpRegionProtection:Regions:WK:0", "198.51.100.0/24")]
    [DataRow("IpRegionProtection:Regions:WK:0", "0.0.0.0/0")]
    [DataRow("IpRegionProtection:Regions:WK:0", "invalid-mask")]
    [DataRow("IpRegionProtection:TrustedProxies:0", "203.0.113.0/24")]
    [DataRow("ASPNETCORE_FORWARDEDHEADERS_ENABLED", "true")]
    public void InvalidConfigurationKeepsRecoveryForOptedInAccountsOnly(string key, string value)
    {
        var policy = Create(key, value);
        Assert.IsTrue(policy.Enabled);
        Assert.IsFalse(policy.Available);
        Assert.IsNotNull(policy.ConfigurationError);
        Assert.IsNull(policy.CreateForwardedHeadersOptions());
        Assert.IsTrue(policy.RequiresRecovery("HM", IPAddress.Parse("198.51.100.42")));
        Assert.IsFalse(policy.RequiresRecovery(null, IPAddress.Parse("198.51.100.42")));
    }

    [TestMethod]
    public async Task ForwardingUsesOnlyOneHopFromAnExactlyConfiguredProxy()
    {
        var policy = Create();
        var settings = policy.CreateForwardedHeadersOptions();
        Assert.IsNotNull(settings);
        Assert.AreEqual(1, settings.ForwardLimit);
        var forwarding = new ForwardedHeadersMiddleware(_ => Task.CompletedTask,
            NullLoggerFactory.Instance, Options.Create(settings));

        var direct = new DefaultHttpContext();
        direct.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.11");
        direct.Request.Headers["X-Forwarded-For"] = "198.51.100.42";
        await ApplyForwarding(direct);
        Assert.IsNull(policy.Resolve(direct.Connection.RemoteIpAddress));

        var noConnectionAddress = new DefaultHttpContext();
        noConnectionAddress.Request.Headers["X-Forwarded-For"] = "198.51.100.42";
        await ApplyForwarding(noConnectionAddress);
        Assert.IsNull(policy.Resolve(noConnectionAddress.Connection.RemoteIpAddress));

        var proxied = new DefaultHttpContext();
        proxied.Connection.RemoteIpAddress = IPAddress.Parse("::ffff:203.0.113.10");
        proxied.Request.Headers["X-Forwarded-For"] = "198.51.100.130, 198.51.100.42";
        proxied.Request.Headers["X-Forwarded-Proto"] = "https";
        await ApplyForwarding(proxied);
        Assert.AreEqual("HM", policy.Resolve(proxied.Connection.RemoteIpAddress));

        var missingForward = new DefaultHttpContext();
        missingForward.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.10");
        await ApplyForwarding(missingForward);
        Assert.IsNull(policy.Resolve(missingForward.Connection.RemoteIpAddress));

        Task ApplyForwarding(HttpContext request) => policy.IsTrustedProxy(request.Connection.RemoteIpAddress)
            ? forwarding.Invoke(request)
            : Task.CompletedTask;
    }

    private static IpRegionPolicy Create(string? overrideKey = null, string? overrideValue = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["IpRegionProtection:Enabled"] = "true",
            ["IpRegionProtection:TrustedProxies:0"] = "203.0.113.10",
            ["IpRegionProtection:Regions:HM:0"] = "198.51.100.0/24",
            ["IpRegionProtection:Regions:WK:0"] = "198.51.100.128/25",
            ["IpRegionProtection:Regions:WK:1"] = "2001:db8:42::/48"
        };
        if (overrideKey is not null)
            values[overrideKey] = overrideValue;

        return IpRegionPolicy.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(values).Build());
    }
}

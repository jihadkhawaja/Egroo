# Sign-in region protection

Server operators can map address ranges to local region codes. Egroo checks the connection address against that map when someone with a saved region restriction signs in with a password. Existing accounts can continue signing in normally until they opt in. Current sessions continue to operate until the usual session rules end them.

## Self-hosted configuration

Enable the feature and provide a local map in the server configuration. The ranges below are reserved for documentation. Replace them with ranges verified for your installation. Region codes use two ASCII letters.

```json
{
  "IpRegionProtection": {
    "Enabled": true,
    "TrustedProxies": ["192.0.2.10"],
    "Regions": {
      "HM": ["198.51.100.0/24", "2001:db8:1::/48"],
      "WK": ["203.0.113.0/24"]
    }
  }
}
```

For a direct connection, use an empty trusted proxy list. For a reverse proxy, list its exact connection address and configure it to set the client address from its own connection in `X-Forwarded-For` and the request scheme in `X-Forwarded-Proto`. Keep automatic forwarded headers disabled. Egroo accepts one forwarded hop from an exact trusted proxy, then matches the most specific IPv4 or IPv6 prefix. An unmapped address or a trusted proxy without a forwarded client address has an unknown region. [ASP.NET Core proxy configuration](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0)

The map lives on the server. Egroo performs its lookup locally and saves the account's allowed region codes. Keep the ranges current as networks and providers change. An invalid enabled map prevents new region opt-ins, and protected sign-ins can still use their recovery codes.

## Account settings and travel

Enroll an authenticator and save the one-time recovery codes. In **Settings**, open **Sign-in regions**, select your current region and any other configured regions you want to allow, then confirm with your password and a fresh authenticator code.

When a connection falls outside your allowed regions or has an unknown region, sign in with your password and an unused recovery code. Then change the list from a mapped region or turn off the restriction from the signed-in settings using your password and an authenticator or unused recovery code. Before traveling, add a configured destination or temporarily turn off region protection.

If you have used your recovery codes, sign in from an allowed connection with your authenticator and replace your two-factor enrollment to obtain new codes. Turning off two-factor authentication also clears saved region restrictions. If a map change leaves a protected account without a mapped allowed connection and the account holder has no unused codes, a self-hosting operator can temporarily disable region enforcement while the account holder recovers access with their existing two-factor credentials.

A verified password reset clears saved region restrictions so the account holder can sign in from their current connection. The reset still requires the enrolled authenticator or an unused recovery code. Two-factor authentication stays enabled.

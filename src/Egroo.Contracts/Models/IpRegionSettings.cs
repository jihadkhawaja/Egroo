namespace Egroo.Contracts.Models;

public static class IpRegionSettings
{
    public sealed record Status(
        bool Available,
        bool Enabled,
        bool CanEnable,
        string? CurrentRegion,
        string[] Regions,
        string[] AllowedRegions);

    public sealed record Update(string Password, string VerificationCode, string[]? AllowedRegions);
}

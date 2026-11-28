using Egroo.Contracts.Interfaces;
using Egroo.Contracts.Models;

namespace Egroo.Server.API;

internal static class IpRegionEndpoint
{
    internal static void MapIpRegionProtection(this RouteGroupBuilder auth)
    {
        var group = auth.MapGroup("/ipregions").RequireAuthorization();
        group.MapGet("/", (IIpRegionAuth authorization) => authorization.GetIpRegionStatus());
        group.MapPut("/", (IIpRegionAuth authorization, IpRegionSettings.Update request) =>
            authorization.UpdateIpRegionSettings(request));
    }
}

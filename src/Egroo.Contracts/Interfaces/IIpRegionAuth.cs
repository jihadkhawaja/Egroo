using Egroo.Contracts.Models;

namespace Egroo.Contracts.Interfaces;

public interface IIpRegionAuth
{
    Task<IpRegionSettings.Status> GetIpRegionStatus();
    Task<Operation.Result> UpdateIpRegionSettings(IpRegionSettings.Update settings);
}

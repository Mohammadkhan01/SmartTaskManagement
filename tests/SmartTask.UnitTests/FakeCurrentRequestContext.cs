using SmartTask.BLL.Interfaces.Services;

namespace SmartTask.UnitTests;

internal sealed class FakeCurrentRequestContext : ICurrentRequestContext
{
    public const string DefaultIpAddress = "198.51.100.7";

    public string? IpAddress { get; set; } = DefaultIpAddress;
}

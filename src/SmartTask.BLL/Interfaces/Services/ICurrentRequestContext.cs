namespace SmartTask.BLL.Interfaces.Services;

public interface ICurrentRequestContext
{
    // Null when no HTTP request is active or the address is unavailable.
    string? IpAddress { get; }
}

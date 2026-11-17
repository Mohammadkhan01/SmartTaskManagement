using SmartTask.BLL.Interfaces.Services;

namespace SmartTask.API.Http;

public sealed class HttpCurrentRequestContext : ICurrentRequestContext
{
    // Matches AuditLogs.IpAddress varchar(45) (IPv6 maximum textual length).
    private const int MaxIpAddressLength = 45;

    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpCurrentRequestContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? IpAddress
    {
        get
        {
            var address = _httpContextAccessor.HttpContext?
                .Connection
                .RemoteIpAddress;

            if (address is null)
                return null;

            if (address.IsIPv4MappedToIPv6)
                address = address.MapToIPv4();

            var text = address.ToString();

            return text.Length <= MaxIpAddressLength
                ? text
                : text[..MaxIpAddressLength];
        }
    }
}

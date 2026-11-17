using System.Net;
using Microsoft.AspNetCore.Http;
using SmartTask.API.Http;

namespace SmartTask.UnitTests;

public class HttpCurrentRequestContextTests
{
    private static HttpCurrentRequestContext CreateContext(IPAddress? address)
    {
        var accessor = new HttpContextAccessor();

        if (address is not null)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Connection.RemoteIpAddress = address;
            accessor.HttpContext = httpContext;
        }

        return new HttpCurrentRequestContext(accessor);
    }

    [Fact]
    public void IpAddress_ShouldBeNull_WhenNoRequestIsActive()
    {
        Assert.Null(CreateContext(null).IpAddress);
    }

    [Fact]
    public void IpAddress_ShouldReturnIPv4Address()
    {
        Assert.Equal(
            "203.0.113.5",
            CreateContext(IPAddress.Parse("203.0.113.5")).IpAddress);
    }

    [Fact]
    public void IpAddress_ShouldUnwrapIPv4MappedIPv6Address()
    {
        Assert.Equal(
            "127.0.0.1",
            CreateContext(IPAddress.Parse("::ffff:127.0.0.1")).IpAddress);
    }

    [Fact]
    public void IpAddress_ShouldFitAuditColumnLength()
    {
        var ip = CreateContext(
            IPAddress.Parse("2001:db8:85a3:1234:5678:8a2e:370:7334")).IpAddress;

        Assert.NotNull(ip);
        Assert.True(ip.Length <= 45);
    }
}

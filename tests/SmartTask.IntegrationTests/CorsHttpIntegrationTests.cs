using Microsoft.AspNetCore.Mvc.Testing;

namespace SmartTask.IntegrationTests;

public sealed class CorsHttpIntegrationTests
{
    private const string TrustedOrigin = "https://localhost:4200";

    [SqlServerFact]
    public async Task Preflight_FromTrustedOrigin_IsAllowedWithCredentials()
    {
        using var response = await SendPreflightAsync(TrustedOrigin);

        Assert.True(response.Headers.TryGetValues(
            "Access-Control-Allow-Origin",
            out var origins));
        Assert.Equal(TrustedOrigin, Assert.Single(origins));
        Assert.True(response.Headers.TryGetValues(
            "Access-Control-Allow-Credentials",
            out var credentials));
        Assert.Equal("true", Assert.Single(credentials));
    }

    [SqlServerFact]
    public async Task Preflight_FromUntrustedOrigin_IsNotAllowed()
    {
        using var response = await SendPreflightAsync("https://evil.example");

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    private static async Task<HttpResponseMessage> SendPreflightAsync(string origin)
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                "SMARTTASK_TEST_CONNECTION_STRING")!;

        await using var factory =
            new InactiveUserJwtWebApplicationFactory(connectionString);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });

        using var request = new HttpRequestMessage(
            HttpMethod.Options,
            "/api/v1/projects");

        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");

        return await client.SendAsync(request);
    }
}

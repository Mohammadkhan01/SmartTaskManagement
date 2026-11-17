using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SmartTask.IntegrationTests;

public sealed class SecurityHeadersHttpIntegrationTests
{
    [SqlServerFact]
    public async Task Responses_IncludeSecurityHeaders_ForSuccessAndErrorResponses()
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

        using var healthResponse = await client.GetAsync("/health");
        using var unauthorizedResponse = await client.GetAsync("/api/v1/projects");

        Assert.Equal(HttpStatusCode.Unauthorized, unauthorizedResponse.StatusCode);

        foreach (var response in new[] { healthResponse, unauthorizedResponse })
        {
            Assert.Equal("nosniff", GetHeader(response, "X-Content-Type-Options"));
            Assert.Equal("DENY", GetHeader(response, "X-Frame-Options"));
            Assert.Equal("no-referrer", GetHeader(response, "Referrer-Policy"));
        }
    }

    private static string GetHeader(HttpResponseMessage response, string name)
    {
        Assert.True(
            response.Headers.TryGetValues(name, out var values),
            $"Missing {name} header.");

        return Assert.Single(values);
    }
}

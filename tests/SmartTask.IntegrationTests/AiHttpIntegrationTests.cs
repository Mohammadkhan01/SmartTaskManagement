using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SmartTask.API.Exceptions;
using SmartTask.BLL.Exceptions;

namespace SmartTask.IntegrationTests;

public sealed class AiHttpIntegrationTests
{
    private const string Route = "/api/v1/ai/enhance-task-description";

    [SqlServerFact]
    public async Task EnhanceTaskDescription_EndToEnd_WithMockProvider()
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                "SMARTTASK_TEST_CONNECTION_STRING")!;

        await using var baseFactory =
            new ProjectTestWebApplicationFactory(connectionString);

        await using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["Ai:Provider"] = "Mock"
                    })));

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });

        var body = new { title = "Fix login", description = "users cant login" };

        // Unauthenticated requests are rejected.
        using (var anonymous = await client.PostAsJsonAsync(Route, body))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        }

        client.DefaultRequestHeaders.Add(
            "X-Test-UserId",
            $"ai-test-{Guid.NewGuid():N}");

        // Valid enhancement.
        using (var ok = await client.PostAsJsonAsync(Route, body))
        {
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

            using var json = JsonDocument.Parse(await ok.Content.ReadAsStringAsync());
            var enhanced = json.RootElement
                .GetProperty("enhancedDescription")
                .GetString();

            Assert.StartsWith("Users cant login.", enhanced);
        }

        // Empty and excessively long input return validation problems.
        using (var empty = await client.PostAsJsonAsync(
            Route,
            new { title = "x", description = "   " }))
        {
            Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
            Assert.Contains(
                "application/problem+json",
                empty.Content.Headers.ContentType?.ToString());
        }

        using (var tooLong = await client.PostAsJsonAsync(
            Route,
            new { description = new string('a', 4001) }))
        {
            Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        }

        // The "Ai" policy allows 10 requests per window per user (appsettings).
        HttpStatusCode lastStatus = HttpStatusCode.OK;

        for (var attempt = 0; attempt < 10; attempt++)
        {
            using var response = await client.PostAsJsonAsync(Route, body);
            lastStatus = response.StatusCode;

            if (lastStatus == HttpStatusCode.TooManyRequests)
            {
                break;
            }
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, lastStatus);
    }

    [Theory]
    [InlineData(typeof(AiProviderUnavailableException), StatusCodes.Status503ServiceUnavailable)]
    [InlineData(typeof(AiInvalidResponseException), StatusCodes.Status502BadGateway)]
    [InlineData(typeof(AiProviderTimeoutException), StatusCodes.Status504GatewayTimeout)]
    public async Task GlobalExceptionHandler_MapsAiFailuresToProblemDetails(
        Type exceptionType,
        int expectedStatus)
    {
        var exception = (Exception)Activator.CreateInstance(
            exceptionType,
            exceptionType == typeof(AiProviderUnavailableException)
                ? [
                    "The AI provider is currently unavailable. Try again later.",
                    new HttpRequestException("secret-host.internal refused")
                ]
                : [new HttpRequestException("secret-host.internal refused")])!;

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var handled = await new GlobalExceptionHandler(
                NullLogger<GlobalExceptionHandler>.Instance)
            .TryHandleAsync(context, exception, CancellationToken.None);

        context.Response.Body.Position = 0;
        var response = await new StreamReader(context.Response.Body).ReadToEndAsync();

        Assert.True(handled);
        Assert.Equal(expectedStatus, context.Response.StatusCode);
        Assert.Contains("application/problem+json", context.Response.ContentType);
        Assert.Contains("Try again later.", response);
        Assert.DoesNotContain("secret-host", response);
    }
}

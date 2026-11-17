using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;

namespace SmartTask.IntegrationTests;

public sealed class AuthRateLimitHttpIntegrationTests
{
    [SqlServerFact]
    public async Task Login_WhenRequestsExceedConfiguredLimit_Returns429()
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                "SMARTTASK_TEST_CONNECTION_STRING")
            ?? throw new InvalidOperationException(
                "SMARTTASK_TEST_CONNECTION_STRING is required.");

        var connectionBuilder =
            new SqlConnectionStringBuilder(connectionString);

        Assert.Equal("SmartTaskTestDb", connectionBuilder.InitialCatalog);

        await using var factory =
            new ProjectTestWebApplicationFactory(connectionString);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });

        for (var requestNumber = 1; requestNumber <= 10; requestNumber++)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "/api/auth/login")
            {
                Content = new StringContent("{}")
            };

            using var response = await client.SendAsync(request);

            Assert.Equal(
                HttpStatusCode.BadRequest,
                response.StatusCode);
        }

        using var eleventhRequest = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/auth/login")
        {
            Content = new StringContent("{}")
        };

        using var eleventhResponse =
            await client.SendAsync(eleventhRequest);

        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            eleventhResponse.StatusCode);
    }
}

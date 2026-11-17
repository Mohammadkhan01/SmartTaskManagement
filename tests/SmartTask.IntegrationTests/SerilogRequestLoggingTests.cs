using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using SmartTask.API.Logging;
using System.Collections.Concurrent;

namespace SmartTask.IntegrationTests;

public class SerilogRequestLoggingTests
{
    [Fact]
    public async Task RequestLogging_Records401AndTraceId_WithoutQueryString()
    {
        var sink = new CollectingSink();
        await using var app = CreateApp(sink);

        app.Run(context =>
        {
            context.Response.StatusCode = 401;
            return Task.CompletedTask;
        });

        await app.StartAsync();

        using var client = app.GetTestClient();
        using var response = await client.GetAsync(
            "/auth/login?email=private@example.com");

        Assert.Equal(401, (int)response.StatusCode);

        var logEvent = await WaitForRequestEventAsync(sink);

        Assert.Equal(LogEventLevel.Warning, logEvent.Level);
        Assert.Equal(401, ((ScalarValue)logEvent.Properties["StatusCode"]).Value);
        Assert.Equal(
            "/auth/login",
            ((ScalarValue)logEvent.Properties["RequestPath"]).Value);
        Assert.True(logEvent.Properties.ContainsKey("TraceId"));

        var loggedData = logEvent.RenderMessage() + " " +
            string.Join(" ", logEvent.Properties.Values);

        Assert.DoesNotContain("private@example.com", loggedData);
    }

    [Fact]
    public async Task RequestLogging_Records500AtErrorLevel_WithException()
    {
        var sink = new CollectingSink();
        var expectedException = new InvalidOperationException(
            "Safe test failure.");

        await using var app = CreateApp(sink);

        app.Run(_ => throw expectedException);

        await app.StartAsync();

        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/test-failure");

        Assert.Equal(500, (int)response.StatusCode);

        var logEvent = await WaitForRequestEventAsync(sink);

        Assert.Equal(LogEventLevel.Error, logEvent.Level);
        Assert.Equal(500, ((ScalarValue)logEvent.Properties["StatusCode"]).Value);
        Assert.Equal(
            "/test-failure",
            ((ScalarValue)logEvent.Properties["RequestPath"]).Value);
        Assert.Same(expectedException, logEvent.Exception);
        Assert.True(logEvent.Properties.ContainsKey("TraceId"));
    }

    [Fact]
    public async Task RequestLogging_HandledClientError_DoesNotAttachException()
    {
        var sink = new CollectingSink();

        await using var app = CreateApp(
            sink,
            exceptionStatusCode: StatusCodes.Status400BadRequest);

        app.Run(_ => throw new ArgumentException("Invalid input."));

        await app.StartAsync();

        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/bad-request");

        Assert.Equal(400, (int)response.StatusCode);

        var logEvent = await WaitForRequestEventAsync(sink);

        Assert.Equal(LogEventLevel.Information, logEvent.Level);
        Assert.Null(logEvent.Exception);
    }

    // The test client can receive the response before the request logging
    // middleware writes its completion event.
    private static async Task<LogEvent> WaitForRequestEventAsync(
        CollectingSink sink)
    {
        static bool IsRequestEvent(LogEvent e) =>
            e.Properties.ContainsKey("RequestMethod") &&
            e.Properties.ContainsKey("RequestPath");

        var deadline = DateTime.UtcNow.AddSeconds(5);

        while (!sink.Events.Any(IsRequestEvent) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        return Assert.Single(sink.Events, IsRequestEvent);
    }

    // Mirrors the production pipeline order: request logging first, then the
    // exception handler that converts exceptions into HTTP responses.
    private static WebApplication CreateApp(
        CollectingSink sink,
        int exceptionStatusCode = StatusCodes.Status500InternalServerError)
    {
        // A dedicated logger avoids the shared static Log.Logger, which other
        // tests running in parallel also replace.
        var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(sink)
            .CreateLogger();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Host.UseSerilog(logger, dispose: true);
        builder.Services.AddProblemDetails();

        var app = builder.Build();

        app.UseSerilogRequestLogging(options =>
        {
            RequestLoggingConfiguration.Configure(options);
            options.Logger = logger;
        });
        app.UseExceptionHandler(handler => handler.Run(context =>
        {
            context.Response.StatusCode = exceptionStatusCode;
            return Task.CompletedTask;
        }));

        return app;
    }

    private sealed class CollectingSink : ILogEventSink
    {
        private readonly ConcurrentQueue<LogEvent> _events = new();

        public IReadOnlyCollection<LogEvent> Events => _events.ToArray();

        public void Emit(LogEvent logEvent)
        {
            _events.Enqueue(logEvent);
        }
    }
}

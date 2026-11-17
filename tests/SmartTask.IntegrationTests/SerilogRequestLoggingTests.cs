using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System.Collections.Concurrent;

namespace SmartTask.IntegrationTests;

public class SerilogRequestLoggingTests
{
    [Fact]
    public async Task RequestLogging_Records401AndTraceId_WithoutQueryString()
    {
        var sink = new CollectingSink();
        await using var app = CreateApp(sink, (context, _, _) =>
            context.Response.StatusCode == 401
                ? LogEventLevel.Warning
                : LogEventLevel.Information);

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

        var logEvent = Assert.Single(
            sink.Events,
            e => e.Properties.ContainsKey("RequestMethod") &&
                 e.Properties.ContainsKey("RequestPath"));

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

        await using var app = CreateApp(sink, (_, _, exception) =>
            exception is not null
                ? LogEventLevel.Error
                : LogEventLevel.Information);

        app.Run(_ => throw expectedException);

        await app.StartAsync();

        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/test-failure");

        Assert.Equal(500, (int)response.StatusCode);

        var logEvent = Assert.Single(
            sink.Events,
            e => e.Properties.ContainsKey("RequestMethod") &&
                 e.Properties.ContainsKey("RequestPath"));

        Assert.Equal(LogEventLevel.Error, logEvent.Level);
        Assert.Equal(
            "/test-failure",
            ((ScalarValue)logEvent.Properties["RequestPath"]).Value);
        Assert.Same(expectedException, logEvent.Exception);
        Assert.True(logEvent.Properties.ContainsKey("TraceId"));
    }

    private static WebApplication CreateApp(
        CollectingSink sink,
        Func<
            Microsoft.AspNetCore.Http.HttpContext,
            double,
            Exception?,
            LogEventLevel> getLevel)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Host.UseSerilog((_, loggerConfiguration) =>
            loggerConfiguration
                .MinimumLevel.Verbose()
                .WriteTo.Sink(sink));

        var app = builder.Build();

        app.UseSerilogRequestLogging(options =>
        {
            options.GetLevel = getLevel;
            options.EnrichDiagnosticContext = (diagnosticContext, context) =>
                diagnosticContext.Set("TraceId", context.TraceIdentifier);
        });

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

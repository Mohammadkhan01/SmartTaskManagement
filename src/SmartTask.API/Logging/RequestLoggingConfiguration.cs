using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Serilog.AspNetCore;
using Serilog.Events;

namespace SmartTask.API.Logging;

public static class RequestLoggingConfiguration
{
    public static void Configure(RequestLoggingOptions options)
    {
        options.GetLevel = GetLevel;
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            var activity = Activity.Current;

            diagnosticContext.Set(
                "TraceId",
                activity?.TraceId.ToString() ?? httpContext.TraceIdentifier);

            if (activity is not null)
            {
                diagnosticContext.Set("SpanId", activity.SpanId.ToString());
            }

            // UseExceptionHandler runs after request logging and swallows the
            // exception, so attach it to the request event for 5xx responses.
            var handledException = httpContext.Features
                .Get<IExceptionHandlerFeature>()?.Error;

            if (handledException is not null &&
                httpContext.Response.StatusCode >=
                    StatusCodes.Status500InternalServerError)
            {
                diagnosticContext.SetException(handledException);
            }
        };
    }

    public static LogEventLevel GetLevel(
        HttpContext httpContext,
        double elapsedMilliseconds,
        Exception? exception)
    {
        if (exception is not null ||
            httpContext.Response.StatusCode >= StatusCodes.Status500InternalServerError)
        {
            return LogEventLevel.Error;
        }

        if (httpContext.Response.StatusCode is
            StatusCodes.Status401Unauthorized or
            StatusCodes.Status403Forbidden)
        {
            return LogEventLevel.Warning;
        }

        return LogEventLevel.Information;
    }
}

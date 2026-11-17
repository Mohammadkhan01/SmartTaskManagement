using SmartTask.DAL;
using SmartTask.BLL;
using SmartTask.API.Exceptions;
using SmartTask.API.Authentication;
using SmartTask.DAL.Identity;
using SmartTask.API.Development;
using Microsoft.Extensions.Options;
using SmartTask.DAL.Storage;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using System.Diagnostics;
using Serilog;
using Serilog.Events;


var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((_, loggerConfiguration) =>
    loggerConfiguration
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "SmartTask.API")
        .WriteTo.Console());

builder.Services.Configure<FileStorageOptions>(options =>
{
    options.RootPath = Path.Combine(
        builder.Environment.ContentRootPath,
        "App_Data",
        "TaskAttachments");
});
// Register services.
builder.Services.AddControllersWithViews();
builder.Services.AddOpenApi();

// Configurable per-IP rate limiting for sensitive authentication endpoints.
var authRateLimitPermitLimit =
    builder.Configuration.GetValue<int>("RateLimiting:Auth:PermitLimit");
var authRateLimitWindowSeconds =
    builder.Configuration.GetValue<int>("RateLimiting:Auth:WindowSeconds");

if (authRateLimitPermitLimit <= 0 || authRateLimitWindowSeconds <= 0)
{
    throw new InvalidOperationException(
        "RateLimiting:Auth:PermitLimit and WindowSeconds must be positive.");
}

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("Auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey:
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = authRateLimitPermitLimit,
                Window = TimeSpan.FromSeconds(authRateLimitWindowSeconds),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

// Cookie-based refresh-token protection.
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "smarttask.csrf";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.Path = "/api/";
});

// Credentialed CORS for explicitly trusted frontend origins.
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>()
    ?? ["http://localhost:4200"];

if (allowedOrigins.Length == 0 ||
    allowedOrigins.Any(origin =>
        string.IsNullOrWhiteSpace(origin) ||
        origin == "*" ||
        !Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
        (uri.Scheme != Uri.UriSchemeHttp &&
         uri.Scheme != Uri.UriSchemeHttps)))
{
    throw new InvalidOperationException(
        "Cors:AllowedOrigins must contain explicit HTTP or HTTPS origins.");
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddHealthChecks()
    .AddCheck(
        "self",
        () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(),
        tags: ["live"])
    .AddCheck<SmartTask.API.HealthChecks.DatabaseHealthCheck>(
        "database",
        tags: ["ready"]);

builder.Services.AddDal(builder.Configuration);
builder.Services.AddBll();

builder.Services.AddJwtAuthentication(builder.Configuration);

var app = builder.Build();
await IdentityRoleSeeder.SeedAsync(app.Services);

if (app.Environment.IsDevelopment() &&
    app.Configuration.GetValue<bool>("DevelopmentAdmin:Enabled"))
{
    await DevelopmentAdminSeeder.SeedAsync(
        app.Services,
        app.Configuration);
}

// Configure the HTTP request pipeline.
app.UseSerilogRequestLogging(options =>
{
    options.GetLevel = (httpContext, _, exception) =>
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
    };

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
    };
});

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseCors("Frontend");

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();

public partial class Program { }
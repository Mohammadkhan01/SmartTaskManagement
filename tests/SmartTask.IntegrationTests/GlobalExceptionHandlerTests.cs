using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using SmartTask.API.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.Features;


namespace SmartTask.IntegrationTests;

public class GlobalExceptionHandlerTests
{
    private static GlobalExceptionHandler CreateHandler()
    {
        return new GlobalExceptionHandler(
            NullLogger<GlobalExceptionHandler>.Instance);
    }

    [Fact]
    public async Task TryHandleAsync_ValidationException_Returns400WithErrors()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var exception = new ValidationException(
        [
            new ValidationFailure(
                "Name",
                "Project name is required.")
        ]);

        var handled = await CreateHandler().TryHandleAsync(
            context,
            exception,
            CancellationToken.None);

        context.Response.Body.Position = 0;

        using var reader = new StreamReader(context.Response.Body);
        var response = await reader.ReadToEndAsync();

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Contains("application/problem+json", context.Response.ContentType);
        Assert.Contains("Name", response);
        Assert.Contains("Project name is required.", response);
        Assert.Contains(context.TraceIdentifier, response);
    }

    [Fact]
    public async Task TryHandleAsync_ArgumentException_Returns400()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var exception = new ArgumentException("Owner ID is required.");

        var handled = await CreateHandler().TryHandleAsync(
            context,
            exception,
            CancellationToken.None);

        context.Response.Body.Position = 0;

        using var reader = new StreamReader(context.Response.Body);
        var response = await reader.ReadToEndAsync();

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Contains("Invalid request", response);
        Assert.Contains("Owner ID is required.", response);
    }

    [Fact]
    public async Task TryHandleAsync_UnexpectedException_Returns500WithoutLeakingDetails()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var exception = new InvalidOperationException(
            "Sensitive internal database details.");

        var handled = await CreateHandler().TryHandleAsync(
            context,
            exception,
            CancellationToken.None);

        context.Response.Body.Position = 0;

        using var reader = new StreamReader(context.Response.Body);
        var response = await reader.ReadToEndAsync();

        Assert.True(handled);
        Assert.Equal(
            StatusCodes.Status500InternalServerError,
            context.Response.StatusCode);

        Assert.Contains("Internal server error", response);
        Assert.DoesNotContain("Sensitive internal database details.", response);
    }

    [Fact]
    public async Task TryHandleAsync_ConcurrencyException_Returns409WithoutLeakingDetails()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var exception = new DbUpdateConcurrencyException(
            "Sensitive database concurrency details.");

        var handled = await CreateHandler().TryHandleAsync(
            context,
            exception,
            CancellationToken.None);

        context.Response.Body.Position = 0;

        using var reader = new StreamReader(context.Response.Body);
        var response = await reader.ReadToEndAsync();

        Assert.True(handled);
        Assert.Equal(
            StatusCodes.Status409Conflict,
            context.Response.StatusCode);

        Assert.Contains("application/problem+json", context.Response.ContentType);
        Assert.Contains("Concurrency conflict", response);
        Assert.Contains("Reload it and try again", response);
        Assert.DoesNotContain("Sensitive database concurrency details.", response);
        Assert.Contains(context.TraceIdentifier, response);
    }


[Fact]
public async Task TryHandleAsync_ResponseAlreadyStarted_ReturnsFalse()
{
    var context = new DefaultHttpContext();

    context.Features.Set<IHttpResponseFeature>(
        new StartedResponseFeature());

    var handled = await CreateHandler().TryHandleAsync(
        context,
        new InvalidOperationException("Test exception."),
        CancellationToken.None);

    Assert.True(context.Response.HasStarted);
    Assert.False(handled);
}
[Fact]
public async Task TryHandleAsync_ClientCancellation_DoesNotWriteErrorResponse()
{
    using var cancellationSource = new CancellationTokenSource();
    cancellationSource.Cancel();

    var context = new DefaultHttpContext
    {
        RequestAborted = cancellationSource.Token
    };

    var responseBody = new MemoryStream();
    context.Response.Body = responseBody;

    var handled = await CreateHandler().TryHandleAsync(
        context,
        new OperationCanceledException("Request cancelled."),
        CancellationToken.None);

    Assert.True(handled);
    //Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    Assert.Equal(0, responseBody.Length);
}
private sealed class StartedResponseFeature : IHttpResponseFeature
{
    public int StatusCode { get; set; } = 200;
    public string? ReasonPhrase { get; set; }
    public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
    public Stream Body { get; set; } = new MemoryStream();
    public bool HasStarted => true;

    public void OnStarting(Func<object, Task> callback, object state)
    {
    }

    public void OnCompleted(Func<object, Task> callback, object state)
    {
    }
}
}

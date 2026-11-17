
using FluentValidation;
using SmartTask.BLL.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SmartTask.API.Exceptions;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Do not attempt to modify a response that has already started.
if (httpContext.Response.HasStarted)
{
    _logger.LogWarning(
        "Cannot handle exception because the response has already started. TraceId: {TraceId}",
        httpContext.TraceIdentifier);

    return false;
}

// A disconnected client may cancel the request.
// Do not generate an error response or log it as an application failure.
if (exception is OperationCanceledException &&
    httpContext.RequestAborted.IsCancellationRequested)
{
    _logger.LogDebug(
        "Request was cancelled by the client. TraceId: {TraceId}",
        httpContext.TraceIdentifier);

    return true;
}
        var problem = exception switch
        {
            ValidationException validationException =>
                CreateValidationProblem(validationException),

            DbUpdateConcurrencyException =>
                new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Concurrency conflict",
                    Detail = "This resource was modified by another request. Reload it and try again."
                },

            ConflictException conflictException =>
                new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Conflict",
                    Detail = conflictException.Message
                },

            ForbiddenAccessException forbiddenException =>
                new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "Forbidden",
                    Detail = forbiddenException.Message
                },

            UnauthorizedAccessException =>
                new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Unauthorized",
                    Detail = "The supplied authentication credentials are invalid or expired."
                },

            ArgumentException argumentException =>
                new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Invalid request",
                    Detail = argumentException.Message
                },

            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal server error",
                Detail = "An unexpected error occurred."
            }
        };

        if (problem.Status == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(
                exception,
                "An unhandled exception occurred. TraceId: {TraceId}",
                httpContext.TraceIdentifier);
        }
        else if (exception is DbUpdateConcurrencyException)
        {
            _logger.LogWarning(
                exception,
                "A concurrency conflict occurred. TraceId: {TraceId}",
                httpContext.TraceIdentifier);
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        httpContext.Response.ContentType = "application/problem+json";

        problem.Extensions["traceId"] = httpContext.TraceIdentifier;

        await System.Text.Json.JsonSerializer.SerializeAsync(
            httpContext.Response.Body,
            problem,
            problem.GetType(),
            cancellationToken: cancellationToken);

        return true;
    }

    private static ValidationProblemDetails CreateValidationProblem(
        ValidationException exception)
    {
        var errors = exception.Errors
            .GroupBy(error => error.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.ErrorMessage).ToArray());

        return new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "One or more validation errors occurred."
        };
    }
}

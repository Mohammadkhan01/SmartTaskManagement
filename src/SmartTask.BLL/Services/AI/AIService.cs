using System.Diagnostics;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartTask.BLL.Configuration;
using SmartTask.BLL.DTOs.AI;
using SmartTask.BLL.Exceptions;
using SmartTask.BLL.Interfaces.Services;

namespace SmartTask.BLL.Services.AI;

// Log statements must never include user content, prompts, model output or keys.
public sealed class AIService : IAIService
{
    private const int DefaultTimeoutSeconds = 20;

    private readonly IEnumerable<IAiTextProvider> _providers;
    private readonly IValidator<EnhanceTaskDescriptionRequest> _validator;
    private readonly AiSettings _settings;
    private readonly ILogger<AIService> _logger;

    public AIService(
        IEnumerable<IAiTextProvider> providers,
        IValidator<EnhanceTaskDescriptionRequest> validator,
        IOptions<AiSettings> options,
        ILogger<AIService> logger)
    {
        _providers = providers;
        _validator = validator;
        _settings = options.Value;
        _logger = logger;
    }

    public async Task<EnhanceTaskDescriptionResponse> EnhanceTaskDescriptionAsync(
        EnhanceTaskDescriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        var provider = SelectProvider();
        var prompt = TaskDescriptionPrompt.Build(request);

        var timeoutSeconds = _settings.TimeoutSeconds > 0
            ? _settings.TimeoutSeconds
            : DefaultTimeoutSeconds;

        using var timeoutSource =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        var stopwatch = Stopwatch.StartNew();
        string rawOutput;

        try
        {
            // WaitAsync enforces the timeout even if a provider ignores the token.
            rawOutput = await provider
                .GenerateAsync(prompt, timeoutSource.Token)
                .WaitAsync(timeoutSource.Token);
        }
        catch (OperationCanceledException exception)
            when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "AI provider {Provider} timed out after {TimeoutSeconds} seconds.",
                provider.Name,
                timeoutSeconds);

            throw new AiProviderTimeoutException(exception);
        }
        catch (AiProviderException exception)
        {
            _logger.LogWarning(
                "AI provider {Provider} failed with {FailureType}.",
                provider.Name,
                exception.GetType().Name);

            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(
                "AI provider {Provider} failed with {FailureType}.",
                provider.Name,
                exception.GetType().Name);

            throw new AiProviderUnavailableException(innerException: exception);
        }

        string enhancedDescription;

        try
        {
            enhancedDescription = TaskDescriptionPrompt.ParseOutput(rawOutput);
        }
        catch (AiInvalidResponseException)
        {
            _logger.LogWarning(
                "AI provider {Provider} returned output that failed validation.",
                provider.Name);

            throw;
        }

        _logger.LogInformation(
            "AI task-description enhancement completed by {Provider} in {ElapsedMilliseconds} ms.",
            provider.Name,
            stopwatch.ElapsedMilliseconds);

        return new EnhanceTaskDescriptionResponse
        {
            EnhancedDescription = enhancedDescription
        };
    }

    private IAiTextProvider SelectProvider()
    {
        var provider = _providers.FirstOrDefault(candidate =>
            string.Equals(
                candidate.Name,
                _settings.Provider,
                StringComparison.OrdinalIgnoreCase));

        if (provider is null)
        {
            _logger.LogWarning(
                "AI provider {Provider} is not registered.",
                _settings.Provider);

            throw new AiProviderUnavailableException(
                "The AI provider is not configured.");
        }

        return provider;
    }
}

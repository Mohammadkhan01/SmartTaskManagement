using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartTask.BLL.Configuration;

namespace SmartTask.DAL.Ai;

/// <summary>
/// Preset for the free GitHub Models service, which exposes an
/// OpenAI-compatible Chat Completions API at
/// https://models.github.ai/inference. Ai:ApiKey must be a GitHub personal
/// access token with the models:read permission. Ai:BaseUrl is ignored and
/// a model id without a publisher prefix (e.g. "gpt-4o-mini") is sent as
/// "openai/gpt-4o-mini".
/// </summary>
public sealed class GitHubModelsTextProvider : OpenAiCompatibleTextProvider
{
    public GitHubModelsTextProvider(
        HttpClient httpClient,
        IOptions<AiSettings> options,
        ILogger<GitHubModelsTextProvider> logger)
        : base(
            AiSettings.GitHubModelsProviderName,
            httpClient,
            CreateSettings(options.Value),
            logger)
    {
    }

    public static AiSettings CreateSettings(AiSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var model = settings.Model?.Trim() ?? string.Empty;

        if (model.Length > 0 && !model.Contains('/'))
        {
            model = $"{AiSettings.GitHubModelsDefaultPublisher}/{model}";
        }

        return new AiSettings
        {
            Provider = AiSettings.GitHubModelsProviderName,
            TimeoutSeconds = settings.TimeoutSeconds,
            ApiKey = settings.ApiKey,
            BaseUrl = AiSettings.GitHubModelsBaseUrl,
            Model = model,
            MaxOutputTokens = settings.MaxOutputTokens,
            Temperature = settings.Temperature,
            UseJsonResponseFormat = settings.UseJsonResponseFormat
        };
    }
}

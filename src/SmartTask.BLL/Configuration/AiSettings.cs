namespace SmartTask.BLL.Configuration;

public sealed class AiSettings
{
    public const string SectionName = "Ai";

    public const string MockProviderName = "Mock";

    public const string OpenAiProviderName = "OpenAI";

    public const string GitHubModelsProviderName = "GitHubModels";

    public const string GitHubModelsBaseUrl = "https://models.github.ai/inference/";

    // GitHub Models requires publisher-prefixed model ids such as "openai/gpt-4o-mini".
    public const string GitHubModelsDefaultPublisher = "openai";

    public string Provider { get; set; } = MockProviderName;

    public int TimeoutSeconds { get; set; } = 20;

    // Never commit a value; supply it through User Secrets or Ai__ApiKey.
    public string? ApiKey { get; set; }

    public string BaseUrl { get; set; } = "https://api.openai.com/v1/";

    public string Model { get; set; } = "gpt-4o-mini";

    public int MaxOutputTokens { get; set; } = 1500;

    public double Temperature { get; set; } = 0.2;

    public bool UseJsonResponseFormat { get; set; } = true;
}

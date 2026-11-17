using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartTask.BLL.Configuration;
using SmartTask.BLL.DTOs.AI;
using SmartTask.BLL.Exceptions;
using SmartTask.BLL.Interfaces.Services;

namespace SmartTask.DAL.Ai;

/// <summary>
/// Calls an OpenAI-compatible Chat Completions endpoint
/// ({BaseUrl}/chat/completions). Works with OpenAI and compatible hosts
/// configured through Ai:BaseUrl and Ai:Model.
/// </summary>
// Log statements must never include prompts, model output or the API key.
public class OpenAiCompatibleTextProvider : IAiTextProvider
{
    private readonly HttpClient _httpClient;
    private readonly AiSettings _settings;
    private readonly ILogger _logger;

    public OpenAiCompatibleTextProvider(
        HttpClient httpClient,
        IOptions<AiSettings> options,
        ILogger<OpenAiCompatibleTextProvider> logger)
        : this(AiSettings.OpenAiProviderName, httpClient, options.Value, logger)
    {
    }

    protected OpenAiCompatibleTextProvider(
        string name,
        HttpClient httpClient,
        AiSettings settings,
        ILogger logger)
    {
        Name = name;
        _httpClient = httpClient;
        _settings = settings;
        _logger = logger;
    }

    public string Name { get; }

    public async Task<string> GenerateAsync(
        AiTextGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(_settings.ApiKey) ||
            string.IsNullOrWhiteSpace(_settings.Model) ||
            !Uri.TryCreate(EnsureTrailingSlash(_settings.BaseUrl), UriKind.Absolute, out var baseUri))
        {
            _logger.LogWarning(
                "AI provider {Provider} is missing Ai:ApiKey, Ai:Model or a valid Ai:BaseUrl.",
                Name);

            throw new AiProviderUnavailableException(
                "The AI provider is not configured.");
        }

        var body = new ChatCompletionRequest
        {
            Model = _settings.Model,
            Temperature = _settings.Temperature,
            MaxTokens = _settings.MaxOutputTokens,
            ResponseFormat = _settings.UseJsonResponseFormat
                ? new ResponseFormat { Type = "json_object" }
                : null,
            Messages =
            [
                new ChatMessage { Role = "system", Content = request.SystemPrompt },
                new ChatMessage { Role = "user", Content = request.UserPrompt }
            ]
        };

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(baseUri, "chat/completions"))
        {
            Content = JsonContent.Create(body)
        };

        httpRequest.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _settings.ApiKey);

        HttpResponseMessage response;

        try
        {
            response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new AiProviderUnavailableException(innerException: exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "AI provider {Provider} returned HTTP {StatusCode}.",
                    Name,
                    (int)response.StatusCode);

                throw new AiProviderUnavailableException();
            }

            try
            {
                var completion = await response.Content
                    .ReadFromJsonAsync<ChatCompletionResponse>(cancellationToken);

                var content = completion?.Choices?.FirstOrDefault()?.Message?.Content;

                if (string.IsNullOrWhiteSpace(content))
                {
                    throw new AiInvalidResponseException();
                }

                return content;
            }
            catch (JsonException exception)
            {
                throw new AiInvalidResponseException(exception);
            }
        }
    }

    private static string EnsureTrailingSlash(string? value) =>
        string.IsNullOrWhiteSpace(value) || value.EndsWith('/')
            ? value ?? string.Empty
            : value + "/";

    private sealed class ChatCompletionRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; init; } = string.Empty;

        [JsonPropertyName("messages")]
        public IReadOnlyList<ChatMessage> Messages { get; init; } = [];

        [JsonPropertyName("temperature")]
        public double Temperature { get; init; }

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; init; }

        [JsonPropertyName("response_format")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ResponseFormat? ResponseFormat { get; init; }
    }

    private sealed class ResponseFormat
    {
        [JsonPropertyName("type")]
        public string Type { get; init; } = string.Empty;
    }

    private sealed class ChatMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; init; } = string.Empty;

        [JsonPropertyName("content")]
        public string? Content { get; init; }
    }

    private sealed class ChatCompletionResponse
    {
        [JsonPropertyName("choices")]
        public IReadOnlyList<ChatChoice>? Choices { get; init; }
    }

    private sealed class ChatChoice
    {
        [JsonPropertyName("message")]
        public ChatMessage? Message { get; init; }
    }
}

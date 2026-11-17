using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmartTask.BLL.Configuration;
using SmartTask.BLL.DTOs.AI;
using SmartTask.BLL.Exceptions;
using SmartTask.BLL.Services.AI;
using SmartTask.DAL.Ai;

namespace SmartTask.UnitTests;

public class AiTextProviderTests
{
    private static readonly AiTextGenerationRequest Prompt =
        TaskDescriptionPrompt.Build(new EnhanceTaskDescriptionRequest
        {
            Title = "Release notes",
            Description = "write release notes for v2"
        });

    [Fact]
    public async Task MockProvider_ReturnsValidStructuredOutput()
    {
        var output = await new MockAiTextProvider().GenerateAsync(Prompt);

        var enhanced = TaskDescriptionPrompt.ParseOutput(output);

        Assert.StartsWith("Write release notes for v2.", enhanced);
    }

    [Fact]
    public void MockProvider_LongMultiLineDescription_IsNotExpanded()
    {
        var description = "first line\nsecond line";

        var enhanced = MockAiTextProvider.Enhance(null, description);

        Assert.Equal("First line\nSecond line.", enhanced);
    }

    [Fact]
    public void MockProvider_MaximumLengthInput_StaysWithinLimit()
    {
        var enhanced = MockAiTextProvider.Enhance(null, new string('a', 4000));

        Assert.Equal(4000, enhanced.Length);
    }

    [Fact]
    public async Task OpenAiProvider_SendsChatCompletionAndReturnsContent()
    {
        var handler = new StubHandler(_ => JsonResponse(HttpStatusCode.OK,
            """{"choices":[{"message":{"role":"assistant","content":"{\"enhancedDescription\":\"Done.\"}"}}]}"""));
        var provider = CreateOpenAiProvider(handler, apiKey: "test-key");

        var output = await provider.GenerateAsync(Prompt);

        Assert.Equal("Done.", TaskDescriptionPrompt.ParseOutput(output));

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://ai.example.test/v1/chat/completions", request.Uri);
        Assert.Equal("Bearer test-key", request.Authorization);

        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal("test-model", body.RootElement.GetProperty("model").GetString());
        Assert.Equal("json_object", body.RootElement
            .GetProperty("response_format").GetProperty("type").GetString());

        var messages = body.RootElement.GetProperty("messages");
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Equal(Prompt.UserPrompt, messages[1].GetProperty("content").GetString());
    }

    [Fact]
    public async Task OpenAiProvider_MissingApiKey_ThrowsUnavailableWithoutCalling()
    {
        var handler = new StubHandler(_ => JsonResponse(HttpStatusCode.OK, "{}"));
        var provider = CreateOpenAiProvider(handler, apiKey: null);

        await Assert.ThrowsAsync<AiProviderUnavailableException>(() =>
            provider.GenerateAsync(Prompt));

        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task OpenAiProvider_ErrorStatus_ThrowsUnavailable(HttpStatusCode statusCode)
    {
        var provider = CreateOpenAiProvider(
            new StubHandler(_ => JsonResponse(statusCode, """{"error":"x"}""")),
            apiKey: "test-key");

        await Assert.ThrowsAsync<AiProviderUnavailableException>(() =>
            provider.GenerateAsync(Prompt));
    }

    [Fact]
    public async Task OpenAiProvider_NetworkFailure_ThrowsUnavailable()
    {
        var provider = CreateOpenAiProvider(
            new StubHandler(_ => throw new HttpRequestException("connection refused")),
            apiKey: "test-key");

        await Assert.ThrowsAsync<AiProviderUnavailableException>(() =>
            provider.GenerateAsync(Prompt));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"choices":[]}""")]
    [InlineData("""{"choices":[{"message":{"content":""}}]}""")]
    public async Task OpenAiProvider_MalformedResponse_ThrowsInvalidResponse(string body)
    {
        var provider = CreateOpenAiProvider(
            new StubHandler(_ => JsonResponse(HttpStatusCode.OK, body)),
            apiKey: "test-key");

        await Assert.ThrowsAsync<AiInvalidResponseException>(() =>
            provider.GenerateAsync(Prompt));
    }

    [Theory]
    [InlineData("gpt-4o-mini", "openai/gpt-4o-mini")]
    [InlineData("openai/gpt-4.1-mini", "openai/gpt-4.1-mini")]
    public async Task GitHubModelsProvider_CallsGitHubModelsEndpointWithBearerToken(
        string configuredModel,
        string expectedModel)
    {
        var handler = new StubHandler(_ => JsonResponse(HttpStatusCode.OK,
            """{"choices":[{"message":{"role":"assistant","content":"{\"enhancedDescription\":\"Done.\"}"}}]}"""));
        var provider = new GitHubModelsTextProvider(
            new HttpClient(handler),
            Options.Create(new AiSettings
            {
                Provider = AiSettings.GitHubModelsProviderName,
                ApiKey = "github-pat",
                BaseUrl = "https://ignored.example.test/v1",
                Model = configuredModel
            }),
            NullLogger<GitHubModelsTextProvider>.Instance);

        var output = await provider.GenerateAsync(Prompt);

        Assert.Equal(AiSettings.GitHubModelsProviderName, provider.Name);
        Assert.Equal("Done.", TaskDescriptionPrompt.ParseOutput(output));

        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://models.github.ai/inference/chat/completions", request.Uri);
        Assert.Equal("Bearer github-pat", request.Authorization);

        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal(expectedModel, body.RootElement.GetProperty("model").GetString());
    }

    [Fact]
    public async Task GitHubModelsProvider_MissingToken_ThrowsUnavailableWithoutCalling()
    {
        var handler = new StubHandler(_ => JsonResponse(HttpStatusCode.OK, "{}"));
        var provider = new GitHubModelsTextProvider(
            new HttpClient(handler),
            Options.Create(new AiSettings
            {
                Provider = AiSettings.GitHubModelsProviderName,
                ApiKey = null
            }),
            NullLogger<GitHubModelsTextProvider>.Instance);

        await Assert.ThrowsAsync<AiProviderUnavailableException>(() =>
            provider.GenerateAsync(Prompt));

        Assert.Empty(handler.Requests);
    }

    private static OpenAiCompatibleTextProvider CreateOpenAiProvider(
        StubHandler handler,
        string? apiKey)
    {
        return new OpenAiCompatibleTextProvider(
            new HttpClient(handler),
            Options.Create(new AiSettings
            {
                Provider = AiSettings.OpenAiProviderName,
                ApiKey = apiKey,
                BaseUrl = "https://ai.example.test/v1",
                Model = "test-model"
            }),
            NullLogger<OpenAiCompatibleTextProvider>.Instance);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string body) =>
        new(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

    private sealed record CapturedRequest(
        HttpMethod Method,
        string? Uri,
        string? Authorization,
        string Body);

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        {
            _respond = respond;
        }

        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = _respond(request);

            Requests.Add(new CapturedRequest(
                request.Method,
                request.RequestUri?.ToString(),
                request.Headers.Authorization?.ToString(),
                request.Content is null
                    ? string.Empty
                    : await request.Content.ReadAsStringAsync(cancellationToken)));

            return response;
        }
    }
}

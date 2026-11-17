namespace SmartTask.BLL.DTOs.AI;

/// <summary>
/// Provider-neutral prompt. Title and Description are the sanitized user
/// values already embedded in <see cref="UserPrompt"/>; they are exposed
/// separately only for deterministic local providers.
/// </summary>
public sealed record AiTextGenerationRequest(
    string SystemPrompt,
    string UserPrompt,
    string Title,
    string Description);

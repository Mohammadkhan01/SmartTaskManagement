using SmartTask.BLL.DTOs.AI;

namespace SmartTask.BLL.Interfaces.Services;

/// <summary>
/// Provider strategy used by <see cref="IAIService"/>. The active provider
/// is selected by matching <see cref="Name"/> against the Ai:Provider setting.
/// </summary>
public interface IAiTextProvider
{
    string Name { get; }

    /// <summary>
    /// Returns the raw model output, expected to be a JSON object of the form
    /// {"enhancedDescription": "..."}. Throws
    /// <see cref="Exceptions.AiProviderUnavailableException"/> when the
    /// provider cannot be reached or is not configured.
    /// </summary>
    Task<string> GenerateAsync(
        AiTextGenerationRequest request,
        CancellationToken cancellationToken = default);
}

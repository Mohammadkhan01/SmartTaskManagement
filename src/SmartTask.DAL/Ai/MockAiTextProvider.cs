using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SmartTask.BLL.Configuration;
using SmartTask.BLL.DTOs.AI;
using SmartTask.BLL.Interfaces.Services;
using SmartTask.BLL.Services.AI;
using SmartTask.BLL.Validators;

namespace SmartTask.DAL.Ai;

/// <summary>
/// Deterministic, offline provider used in Development and tests. It tidies
/// whitespace, capitalization and punctuation, and adds acceptance criteria
/// to short descriptions. It never calls an external service.
/// </summary>
public sealed partial class MockAiTextProvider : IAiTextProvider
{
    private const int ShortDescriptionThreshold = 160;

    public string Name => AiSettings.MockProviderName;

    public Task<string> GenerateAsync(
        AiTextGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var enhanced = Enhance(request.Title, request.Description);

        var output = JsonSerializer.Serialize(
            new Dictionary<string, string>
            {
                [TaskDescriptionPrompt.OutputPropertyName] = enhanced
            });

        return Task.FromResult(output);
    }

    public static string Enhance(string? title, string description)
    {
        var maxLength = EnhanceTaskDescriptionRequestValidator.MaxDescriptionLength;

        var lines = description
            .Split('\n')
            .Select(line => MultipleSpaces().Replace(line, " ").Trim())
            .ToList();

        var text = MultipleBlankLines()
            .Replace(string.Join('\n', lines), "\n\n")
            .Trim();

        if (text.Length == 0)
        {
            return text;
        }

        text = CapitalizeSentences(text);
        text = StandalonePronoun().Replace(text, "I");

        if (text.Length < maxLength && char.IsLetterOrDigit(text[^1]))
        {
            text += ".";
        }

        if (text.Length >= ShortDescriptionThreshold || text.Contains('\n'))
        {
            return text;
        }

        var cleanTitle = string.IsNullOrWhiteSpace(title)
            ? null
            : MultipleSpaces().Replace(title.Trim(), " ");

        var firstCriterion = cleanTitle is null
            ? "- The work described above is completed."
            : $"- \"{cleanTitle}\" is completed as described above.";

        var expanded = new StringBuilder(text)
            .Append("\n\nAcceptance criteria:\n")
            .Append(firstCriterion)
            .Append("\n- The result is reviewed and verified before the task is closed.")
            .ToString();

        return expanded.Length <= maxLength ? expanded : text;
    }

    private static string CapitalizeSentences(string text)
    {
        var characters = text.ToCharArray();
        var capitalizeNext = true;

        for (var index = 0; index < characters.Length; index++)
        {
            var character = characters[index];

            if (capitalizeNext && char.IsLetter(character))
            {
                characters[index] = char.ToUpperInvariant(character);
                capitalizeNext = false;
            }
            else if (character is '.' or '!' or '?' or '\n')
            {
                capitalizeNext = true;
            }
            else if (!char.IsWhiteSpace(character) && character is not '-' and not '*')
            {
                capitalizeNext = false;
            }
        }

        return new string(characters);
    }

    [GeneratedRegex("[ \t]{2,}")]
    private static partial Regex MultipleSpaces();

    [GeneratedRegex("\n{3,}")]
    private static partial Regex MultipleBlankLines();

    [GeneratedRegex(@"(?<=^|\s)i(?=\s|$|[.,!?;:'])", RegexOptions.Multiline)]
    private static partial Regex StandalonePronoun();
}

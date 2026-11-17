namespace SmartTask.BLL.Exceptions;

/// <summary>
/// Base type for AI provider failures. Messages are returned to clients,
/// so they must never contain user content, provider payloads or secrets.
/// </summary>
public abstract class AiProviderException : Exception
{
    protected AiProviderException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

public sealed class AiProviderUnavailableException : AiProviderException
{
    public AiProviderUnavailableException(
        string message = "The AI provider is currently unavailable. Try again later.",
        Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

public sealed class AiProviderTimeoutException : AiProviderException
{
    public AiProviderTimeoutException(Exception? innerException = null)
        : base("The AI provider did not respond in time. Try again later.", innerException)
    {
    }
}

public sealed class AiInvalidResponseException : AiProviderException
{
    public AiInvalidResponseException(Exception? innerException = null)
        : base("The AI provider returned an invalid response. Try again later.", innerException)
    {
    }
}

using Microsoft.Extensions.Options;
using SmartTask.BLL.Interfaces.Services;

namespace SmartTask.DAL.Storage;

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _rootPath;

    public LocalFileStorageService(
        IOptions<FileStorageOptions> options)
    {
        var configuredPath = options.Value.RootPath;

        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            throw new InvalidOperationException(
                "File storage root path is not configured.");
        }

        _rootPath = Path.GetFullPath(configuredPath);

        Directory.CreateDirectory(_rootPath);
    }

    public async Task SaveAsync(
        Stream fileStream,
        string storedFileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileStream);

        var filePath = GetSafeFilePath(storedFileName);
        var fileCreated = false;

        try
        {
            await using var destination = new FileStream(
                filePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true);

            fileCreated = true;

            await fileStream.CopyToAsync(
                destination,
                cancellationToken);
        }
        catch
        {
            if (fileCreated)
            {
                try
                {
                    File.Delete(filePath);
                }
                catch (IOException)
                {
                    // Preserve the original write exception.
                }
                catch (UnauthorizedAccessException)
                {
                    // Preserve the original write exception.
                }
            }

            throw;
        }
    }

    public Task<Stream?> OpenReadAsync(
        string storedFileName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var filePath = GetSafeFilePath(storedFileName);

        if (!File.Exists(filePath))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);

        return Task.FromResult<Stream?>(stream);
    }

    public Task DeleteAsync(
        string storedFileName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var filePath = GetSafeFilePath(storedFileName);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        return Task.CompletedTask;
    }

    private string GetSafeFilePath(string storedFileName)
    {
        if (string.IsNullOrWhiteSpace(storedFileName) ||
            storedFileName.Length is not (36 or 37) ||
            storedFileName.Contains('/') ||
            storedFileName.Contains('\\') ||
            !Guid.TryParseExact(
                Path.GetFileNameWithoutExtension(storedFileName),
                "N",
                out _) ||
            Path.GetExtension(storedFileName).ToLowerInvariant() is not
                (".pdf" or ".png" or ".jpg" or ".jpeg" or ".txt"))
        {
            throw new ArgumentException(
                "Invalid stored file name.",
                nameof(storedFileName));
        }

        return Path.Combine(_rootPath, storedFileName);
    }
}

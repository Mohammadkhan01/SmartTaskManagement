namespace SmartTask.BLL.Interfaces.Services;

public interface IFileStorageService
{
    Task SaveAsync(
        Stream fileStream,
        string storedFileName,
        CancellationToken cancellationToken = default);

    Task<Stream?> OpenReadAsync(
        string storedFileName,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string storedFileName,
        CancellationToken cancellationToken = default);
}

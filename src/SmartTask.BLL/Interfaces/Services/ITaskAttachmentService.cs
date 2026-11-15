
using SmartTask.BLL.DTOs.TaskAttachments;

namespace SmartTask.BLL.Interfaces.Services;

public interface ITaskAttachmentService
{
    Task<TaskAttachmentResponse?> UploadAsync(
        Guid taskId,
        Stream fileStream,
        string originalFileName,
        string contentType,
        long fileSize,
        string userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TaskAttachmentResponse>?> GetByTaskIdAsync(
        Guid taskId,
        string userId,
        CancellationToken cancellationToken = default);

    Task<TaskAttachmentResponse?> GetByIdAsync(
        Guid attachmentId,
        string userId,
        CancellationToken cancellationToken = default);

    Task<TaskAttachmentDownload?> OpenDownloadAsync(
        Guid attachmentId,
        string userId,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        Guid attachmentId,
        string userId,
        CancellationToken cancellationToken = default);
}

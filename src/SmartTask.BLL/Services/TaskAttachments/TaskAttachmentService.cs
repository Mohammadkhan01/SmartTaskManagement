using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SmartTask.BLL.DTOs.TaskAttachments;
using SmartTask.BLL.Interfaces.Repositories;
using SmartTask.BLL.Interfaces.Services;
using SmartTask.Common.Constants;
using SmartTask.Domain.Entities;
using SmartTask.Domain.Enums;

namespace SmartTask.BLL.Services.TaskAttachments;

public class TaskAttachmentService : ITaskAttachmentService
{
    private const long MaxFileSize = 10 * 1024 * 1024;

    private readonly ITaskAttachmentRepository _attachmentRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly IProjectMemberRepository _memberRepository;
    private readonly IFileStorageService _fileStorage;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly ICurrentRequestContext _requestContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TaskAttachmentService> _logger;

    public TaskAttachmentService(
        ITaskAttachmentRepository attachmentRepository,
        ITaskRepository taskRepository,
        IProjectMemberRepository memberRepository,
        IFileStorageService fileStorage,
        IAuditLogRepository auditLogRepository,
        IUnitOfWork unitOfWork,
        ILogger<TaskAttachmentService> logger,
        ICurrentRequestContext requestContext)
    {
        _attachmentRepository = attachmentRepository;
        _taskRepository = taskRepository;
        _memberRepository = memberRepository;
        _fileStorage = fileStorage;
        _auditLogRepository = auditLogRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _requestContext = requestContext;
    }

    public async Task<TaskAttachmentResponse?> UploadAsync(
        Guid taskId,
        Stream fileStream,
        string originalFileName,
        string contentType,
        long fileSize,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (taskId == Guid.Empty ||
            string.IsNullOrWhiteSpace(userId) ||
            fileStream is null ||
            !fileStream.CanRead)
        {
            return null;
        }

        var task = await _taskRepository.GetByIdAsync(
            taskId,
            cancellationToken);

        if (task is null || !await IsMemberAsync(
                task.ProjectId, userId, cancellationToken))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(originalFileName) ||
            string.IsNullOrWhiteSpace(contentType))
        {
            throw new ArgumentException("File name and content type are required.");
        }

        if (!fileStream.CanSeek)
        {
            throw new ArgumentException("The uploaded file stream must support seeking.");
        }

        if (fileStream.Position != 0 ||
            fileStream.Length != fileSize ||
            fileSize <= 0 ||
            fileSize > MaxFileSize)
        {
            throw new ArgumentException(
                "The uploaded file size is invalid or exceeds the 10 MB limit.");
        }

        var extension = Path.GetExtension(originalFileName)
            .ToLowerInvariant();

        var normalizedContentType = contentType
            .Split(';', 2)[0]
            .Trim()
            .ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(extension) ||
            !IsAllowedFileType(extension, normalizedContentType))
        {
            throw new ArgumentException(
                "The uploaded file type is not supported.");
        }

        var originalPosition = fileStream.Position;

        try
        {
            if (!await HasValidSignatureAsync(
                    fileStream,
                    extension,
                    cancellationToken))
            {
                throw new ArgumentException("The uploaded file content does not match its declared file type.");
            }
        }
        finally
        {
            fileStream.Position = originalPosition;
        }

        var safeOriginalFileName = Path.GetFileName(originalFileName.Replace('\\', '/'));

        safeOriginalFileName = new string(
            safeOriginalFileName
                .Where(character => !char.IsControl(character))
                .Take(255)
                .ToArray());

        if (string.IsNullOrWhiteSpace(safeOriginalFileName))
        {
            throw new ArgumentException("The original file name is invalid.");
        }

        var storedFileName = $"{Guid.NewGuid():N}{extension}";

        var attachment = new TaskAttachment
        {
            TaskId = taskId,
            UploadedByUserId = userId,
            OriginalFileName = safeOriginalFileName,
            StoredFileName = storedFileName,
            ContentType = normalizedContentType,
            FileSize = fileSize,
            StoragePath = storedFileName
        };

        await _fileStorage.SaveAsync(
            fileStream,
            storedFileName,
            cancellationToken);

        try
        {
            _attachmentRepository.Add(attachment);

            AddActivity(taskId, userId, AuditActions.AttachmentUploaded, attachment.Id);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            try
            {
                await _fileStorage.DeleteAsync(
                    storedFileName,
                    CancellationToken.None);
            }
            catch
            {
                // Preserve the original database exception.
            }

            throw;
        }

        return MapToResponse(attachment);
    }

    public async Task<IReadOnlyList<TaskAttachmentResponse>?> GetByTaskIdAsync(
        Guid taskId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (taskId == Guid.Empty || string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var task = await _taskRepository.GetByIdAsync(
            taskId,
            cancellationToken);

        if (task is null || !await IsMemberAsync(
                task.ProjectId, userId, cancellationToken))
        {
            return null;
        }

        var attachments = await _attachmentRepository.GetByTaskIdAsync(
            taskId,
            cancellationToken);

        return attachments.Select(MapToResponse).ToList();
    }

    public async Task<TaskAttachmentResponse?> GetByIdAsync(
        Guid attachmentId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var access = await FindAccessibleAttachmentAsync(
            attachmentId,
            userId,
            cancellationToken);

        return access is null ? null : MapToResponse(access.Value.Attachment);
    }

    public async Task<TaskAttachmentDownload?> OpenDownloadAsync(
        Guid attachmentId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var access = await FindAccessibleAttachmentAsync(
            attachmentId,
            userId,
            cancellationToken);

        if (access is null)
        {
            return null;
        }

        var attachment = access.Value.Attachment;

        var stream = await _fileStorage.OpenReadAsync(
            attachment.StoredFileName,
            cancellationToken);

        if (stream is null)
        {
            return null;
        }

        return new TaskAttachmentDownload
        {
            Stream = stream,
            OriginalFileName = attachment.OriginalFileName,
            ContentType = attachment.ContentType
        };
    }

    public async Task<bool> DeleteAsync(
        Guid attachmentId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var access = await FindAccessibleAttachmentAsync(
            attachmentId,
            userId,
            cancellationToken);

        if (access is null)
        {
            return false;
        }

        var (attachment, membership) = access.Value;

        var isUploader = attachment.UploadedByUserId == userId;
        var isManager = membership.ProjectRole == ProjectMemberRole.Manager;

        if (!isUploader && !isManager)
        {
            return false;
        }

        attachment.IsDeleted = true;
        attachment.DeletedAtUtc = DateTime.UtcNow;

        AddActivity(attachment.TaskId, userId, AuditActions.AttachmentDeleted, attachment.Id);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            await _fileStorage.DeleteAsync(
                attachment.StoredFileName,
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to delete physical file for soft-deleted attachment {AttachmentId}.",
                attachment.Id);
        }

        return true;
    }

    // Returns null when the attachment, its task, or the caller's project membership is missing,
    // so callers respond 404 without revealing whether the attachment exists.
    private async Task<(TaskAttachment Attachment, ProjectMember Membership)?> FindAccessibleAttachmentAsync(
        Guid attachmentId,
        string userId,
        CancellationToken cancellationToken)
    {
        if (attachmentId == Guid.Empty || string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var attachment = await _attachmentRepository.GetByIdAsync(
            attachmentId,
            cancellationToken);

        if (attachment is null)
        {
            return null;
        }

        var task = await _taskRepository.GetByIdAsync(
            attachment.TaskId,
            cancellationToken);

        if (task is null)
        {
            return null;
        }

        var membership = await _memberRepository.GetByProjectAndUserAsync(
            task.ProjectId,
            userId,
            cancellationToken);

        return membership is null ? null : (attachment, membership);
    }

    private async Task<bool> IsMemberAsync(
        Guid projectId,
        string userId,
        CancellationToken cancellationToken)
    {
        var membership = await _memberRepository.GetByProjectAndUserAsync(
            projectId,
            userId,
            cancellationToken);

        return membership is not null;
    }

    private void AddActivity(
        Guid taskId,
        string userId,
        string action,
        Guid attachmentId)
    {
        _auditLogRepository.Add(new AuditLog
        {
            UserId = userId,
            Action = action,
            EntityName = "Task",
            EntityId = taskId.ToString(),
            Details = JsonSerializer.Serialize(
                new { AttachmentId = attachmentId }),
            IpAddress = _requestContext.IpAddress
        });
    }

    private static bool IsAllowedFileType(
        string extension,
        string contentType)
    {
        return (extension, contentType) switch
        {
            (".pdf", "application/pdf") => true,
            (".png", "image/png") => true,
            (".jpg" or ".jpeg", "image/jpeg") => true,
            (".txt", "text/plain") => true,
            _ => false
        };
    }

    private static async Task<bool> HasValidSignatureAsync(
        Stream stream,
        string extension,
        CancellationToken cancellationToken)
    {
        if (extension == ".txt")
        {
            try
            {
                using var reader = new StreamReader(
                    stream,
                    new UTF8Encoding(false, true),
                    detectEncodingFromByteOrderMarks: false,
                    bufferSize: 4096,
                    leaveOpen: true);

                var buffer = new char[4096];
                int charsRead;

                while ((charsRead = await reader.ReadAsync(
                    buffer.AsMemory(),
                    cancellationToken)) > 0)
                {
                    if (buffer.AsSpan(0, charsRead).Contains('\0'))
                    {
                        return false;
                    }
                }

                return true;
            }
            catch (DecoderFallbackException)
            {
                return false;
            }
        }

        var header = new byte[8];
        var bytesRead = await stream.ReadAsync(
            header.AsMemory(),
            cancellationToken);

        return extension switch
        {
            ".pdf" => bytesRead >= 5 &&
                Encoding.ASCII.GetString(header, 0, 5) == "%PDF-",

            ".png" => bytesRead >= 8 &&
                header.AsSpan(0, 8).SequenceEqual(
                    new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),

            ".jpg" or ".jpeg" => bytesRead >= 3 &&
                header[0] == 0xFF &&
                header[1] == 0xD8 &&
                header[2] == 0xFF,

            _ => false
        };
    }

    private static TaskAttachmentResponse MapToResponse(
        TaskAttachment attachment)
    {
        return new TaskAttachmentResponse
        {
            Id = attachment.Id,
            TaskId = attachment.TaskId,
            OriginalFileName = attachment.OriginalFileName,
            ContentType = attachment.ContentType,
            FileSize = attachment.FileSize,
            UploadedByUserId = attachment.UploadedByUserId,
            CreatedAtUtc = attachment.CreatedAtUtc
        };
    }
}

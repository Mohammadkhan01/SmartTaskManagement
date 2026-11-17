using Microsoft.Extensions.Logging.Abstractions;
using SmartTask.BLL.Interfaces.Repositories;
using SmartTask.BLL.Interfaces.Services;
using SmartTask.BLL.Services.TaskAttachments;
using SmartTask.Domain.Entities;
using SmartTask.Domain.Enums;

namespace SmartTask.UnitTests;

public class TaskAttachmentServiceTests
{
    private readonly FakeAttachmentRepository _attachments = new();
    private readonly FakeTaskRepository _tasks = new();
    private readonly FakeProjectMemberRepository _members = new();
    private readonly FakeFileStorageService _fileStorage = new();
    private readonly FakeAuditLogRepository _auditLogs = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private TaskAttachmentService CreateService() =>
        new(
            _attachments,
            _tasks,
            _members,
            _fileStorage,
            _auditLogs,
            _unitOfWork,
            NullLogger<TaskAttachmentService>.Instance,
            new FakeCurrentRequestContext());

    private TaskItem CreateTask()
    {
        var task = new TaskItem
        {
            ProjectId = Guid.NewGuid(),
            Title = "Test task",
            CreatedByUserId = "manager-123"
        };

        _tasks.AddedTasks.Add(task);
        return task;
    }

    private void AddMember(
        Guid projectId,
        string userId,
        ProjectMemberRole role = ProjectMemberRole.Member)
    {
        _members.AddedMembers.Add(new ProjectMember
        {
            ProjectId = projectId,
            UserId = userId,
            ProjectRole = role
        });
    }

    private TaskAttachment CreateAttachment(
        Guid taskId,
        string uploadedByUserId = "uploader-123")
    {
        var attachment = new TaskAttachment
        {
            TaskId = taskId,
            UploadedByUserId = uploadedByUserId,
            OriginalFileName = "report.pdf",
            StoredFileName = $"{Guid.NewGuid():N}.pdf",
            StoragePath = "internal-storage-path",
            ContentType = "application/pdf",
            FileSize = 9
        };

        _attachments.Add(attachment);
        return attachment;
    }

    [Fact]
    public async Task UploadAsync_ShouldSaveValidPdfForProjectMember()
    {
        var task = CreateTask();
        AddMember(task.ProjectId, "member-123");

        var content = System.Text.Encoding.ASCII.GetBytes("%PDF-1.7\n");
        await using var stream = new MemoryStream(content);

        var result = await CreateService().UploadAsync(
            task.Id,
            stream,
            @"C:\fakepath\report.pdf",
            "application/pdf; charset=binary",
            content.Length,
            "member-123");

        Assert.NotNull(result);
        Assert.Equal(task.Id, result.TaskId);
        Assert.Equal("report.pdf", result.OriginalFileName);
        Assert.Equal("application/pdf", result.ContentType);
        Assert.Equal(content.Length, result.FileSize);
        Assert.Equal("member-123", result.UploadedByUserId);

        var attachment = Assert.Single(_attachments.AddedAttachments);
        Assert.Equal(task.Id, attachment.TaskId);
        Assert.DoesNotContain(
            "internal-storage-path",
            result.GetType().GetProperties().Select(p => p.Name));

        Assert.Single(_fileStorage.SavedFiles);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);

        var activity = Assert.Single(_auditLogs.AddedLogs);
        Assert.Equal("AttachmentUploaded", activity.Action);
        Assert.Equal(task.Id.ToString(), activity.EntityId);
        Assert.Contains(attachment.Id.ToString(), activity.Details);
    }

    [Fact]

    public async Task UploadAsync_ShouldRejectNonMember()
    {
        var task = CreateTask();
        await using var stream = new MemoryStream(
            System.Text.Encoding.ASCII.GetBytes("%PDF-1.7\n"));

        var result = await CreateService().UploadAsync(
            task.Id,
            stream,
            "report.pdf",
            "application/pdf",
            stream.Length,
            "outsider-123");

        Assert.Null(result);
        Assert.Empty(_attachments.AddedAttachments);
        Assert.Empty(_fileStorage.SavedFiles);
        Assert.Equal(0, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task UploadAsync_ShouldRejectInvalidPdfSignature()
    {
        var task = CreateTask();
        AddMember(task.ProjectId, "member-123");

        await using var stream = new MemoryStream(
            System.Text.Encoding.ASCII.GetBytes("This is not a PDF"));

        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateService().UploadAsync(
                task.Id,
                stream,
                "report.pdf",
                "application/pdf",
                stream.Length,
                "member-123"));

        Assert.Empty(_attachments.AddedAttachments);
        Assert.Empty(_fileStorage.SavedFiles);
        Assert.Equal(0, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task UploadAsync_ShouldRejectMimeTypeThatDoesNotMatchExtension()
    {
        var task = CreateTask();
        AddMember(task.ProjectId, "member-123");

        await using var stream = new MemoryStream(
            System.Text.Encoding.ASCII.GetBytes("%PDF-1.7\\n"));

        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateService().UploadAsync(
                task.Id,
                stream,
                "report.pdf",
                "image/png",
                stream.Length,
                "member-123"));

        Assert.Empty(_attachments.AddedAttachments);
        Assert.Empty(_fileStorage.SavedFiles);
        Assert.Equal(0, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task UploadAsync_ShouldRejectEmptyFile()
    {
        var task = CreateTask();
        AddMember(task.ProjectId, "member-123");

        await using var stream = new MemoryStream();

        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateService().UploadAsync(
                task.Id,
                stream,
                "empty.pdf",
                "application/pdf",
                0,
                "member-123"));

        Assert.Empty(_attachments.AddedAttachments);
        Assert.Empty(_fileStorage.SavedFiles);
        Assert.Equal(0, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task UploadAsync_ShouldRejectTextFileWithInvalidUtf8()
    {
        var task = CreateTask();
        AddMember(task.ProjectId, "member-123");

        await using var stream = new MemoryStream(
            new byte[] { 0x48, 0x69, 0xFF, 0xFE });

        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateService().UploadAsync(
                task.Id,
                stream,
                "notes.txt",
                "text/plain",
                stream.Length,
                "member-123"));

        Assert.Empty(_attachments.AddedAttachments);
        Assert.Empty(_fileStorage.SavedFiles);
        Assert.Equal(0, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task UploadAsync_ShouldRejectTextFileContainingNullByte()
    {
        var task = CreateTask();
        AddMember(task.ProjectId, "member-123");

        await using var stream = new MemoryStream(
            new byte[] { 0x48, 0x69, 0x00, 0x21 });

        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateService().UploadAsync(
                task.Id,
                stream,
                "notes.txt",
                "text/plain",
                stream.Length,
                "member-123"));

        Assert.Empty(_attachments.AddedAttachments);
        Assert.Empty(_fileStorage.SavedFiles);
        Assert.Equal(0, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task UploadAsync_ShouldRejectFileLargerThanTenMegabytes()
    {
        var task = CreateTask();
        AddMember(task.ProjectId, "member-123");

        await using var stream = new MemoryStream(
            new byte[(10 * 1024 * 1024) + 1]);

        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateService().UploadAsync(
                task.Id,
                stream,
                "large.pdf",
                "application/pdf",
                stream.Length,
                "member-123"));

        Assert.Empty(_fileStorage.SavedFiles);
        Assert.Equal(0, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task UploadAsync_ShouldDeletePhysicalFileWhenDatabaseSaveFails()
    {
        var task = CreateTask();
        AddMember(task.ProjectId, "member-123");

        _unitOfWork.SaveChangesException =
            new InvalidOperationException("Database failure");

        await using var stream = new MemoryStream(
            System.Text.Encoding.ASCII.GetBytes("%PDF-1.7\n"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService().UploadAsync(
                task.Id,
                stream,
                "report.pdf",
                "application/pdf",
                stream.Length,
                "member-123"));

        Assert.Empty(_fileStorage.SavedFiles);
        Assert.Single(_fileStorage.DeletedFileNames);
        Assert.Single(_attachments.AddedAttachments);
    }

    [Fact]
    public async Task GetByTaskIdAsync_ShouldReturnOnlyActiveAttachmentsForProjectMember()
    {
        var task = CreateTask();
        AddMember(task.ProjectId, "member-123");

        var activeAttachment = CreateAttachment(task.Id);
        var deletedAttachment = CreateAttachment(task.Id);
        deletedAttachment.IsDeleted = true;
        deletedAttachment.DeletedAtUtc = DateTime.UtcNow;

        var result = await CreateService().GetByTaskIdAsync(
            task.Id,
            "member-123");

        Assert.NotNull(result);
        var response = Assert.Single(result);
        Assert.Equal(activeAttachment.Id, response.Id);
        Assert.DoesNotContain(result, item => item.Id == deletedAttachment.Id);
    }

    [Fact]
    public async Task GetByTaskIdAsync_ShouldRejectNonMember()
    {
        var task = CreateTask();
        AddMember(task.ProjectId, "member-123");
        CreateAttachment(task.Id);

        var result = await CreateService().GetByTaskIdAsync(
            task.Id,
            "outsider-123");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldRejectNonMember()
    {
        var task = CreateTask();
        AddMember(task.ProjectId, "member-123");

        var attachment = CreateAttachment(task.Id);

        var result = await CreateService().GetByIdAsync(
            attachment.Id,
            "outsider-123");

        Assert.Null(result);
        Assert.Empty(_fileStorage.SavedFiles);
        Assert.Equal(0, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task OpenDownloadAsync_ShouldRejectNonMemberWithoutAccessingStorage()
    {
        var task = CreateTask();
        AddMember(task.ProjectId, "member-123");

        var attachment = CreateAttachment(task.Id);

        var result = await CreateService().OpenDownloadAsync(
            attachment.Id,
            "outsider-123");

        Assert.Null(result);
        Assert.Equal(0, _fileStorage.OpenReadCallCount);
    }

    [Fact]
public async Task OpenDownloadAsync_ShouldReturnFileForProjectMember()
{
    var task = CreateTask();
    AddMember(task.ProjectId, "member-123");

    var attachment = CreateAttachment(task.Id);
    var expectedContent = System.Text.Encoding.ASCII.GetBytes("%PDF-1.7\n");
    _fileStorage.SavedFiles[attachment.StoredFileName] = expectedContent;

    var result = await CreateService().OpenDownloadAsync(
        attachment.Id,
        "member-123");

    Assert.NotNull(result);
    Assert.Equal(attachment.OriginalFileName, result.OriginalFileName);
    Assert.Equal(attachment.ContentType, result.ContentType);

    await using (result.Stream)
    {
        using var memoryStream = new MemoryStream();
        await result.Stream.CopyToAsync(memoryStream);

        Assert.Equal(expectedContent, memoryStream.ToArray());
    }

    Assert.Equal(1, _fileStorage.OpenReadCallCount);
}

    [Fact]
    public async Task OpenDownloadAsync_ShouldReturnNullWhenPhysicalFileIsMissing()
    {
        var task = CreateTask();
        AddMember(task.ProjectId, "member-123");

        var attachment = CreateAttachment(task.Id);

        var result = await CreateService().OpenDownloadAsync(
            attachment.Id,
            "member-123");

        Assert.Null(result);
        Assert.Equal(1, _fileStorage.OpenReadCallCount);
    }

    [Fact]
    public async Task OpenDownloadAsync_ShouldRejectSoftDeletedAttachment()
    {
        var task = CreateTask();
        AddMember(task.ProjectId, "member-123");

        var attachment = CreateAttachment(task.Id);
        attachment.IsDeleted = true;
        attachment.DeletedAtUtc = DateTime.UtcNow;

        var result = await CreateService().OpenDownloadAsync(
            attachment.Id,
            "member-123");

        Assert.Null(result);
        Assert.Equal(0, _fileStorage.OpenReadCallCount);
    }

    [Fact]
    public async Task DeleteAsync_ShouldAllowUploaderToDeleteAttachment()
    {
        var task = CreateTask();
        AddMember(task.ProjectId, "uploader-123");

        var attachment = CreateAttachment(task.Id);

        var result = await CreateService().DeleteAsync(
            attachment.Id,
            "uploader-123");

        Assert.True(result);
        Assert.True(attachment.IsDeleted);
        Assert.NotNull(attachment.DeletedAtUtc);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
        Assert.Contains(
            attachment.StoredFileName,
            _fileStorage.DeletedFileNames);

        var activity = Assert.Single(_auditLogs.AddedLogs);
        Assert.Equal("AttachmentDeleted", activity.Action);
        Assert.Equal(task.Id.ToString(), activity.EntityId);
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemainSuccessfulWhenPhysicalFileCleanupFails()
    {
        var task = CreateTask();
        AddMember(task.ProjectId, "uploader-123");

        var attachment = CreateAttachment(task.Id);
        _fileStorage.DeleteException = new IOException("Storage unavailable.");

        var result = await CreateService().DeleteAsync(
            attachment.Id,
            "uploader-123");

        Assert.True(result);
        Assert.True(attachment.IsDeleted);
        Assert.NotNull(attachment.DeletedAtUtc);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
        Assert.Contains(attachment.StoredFileName, _fileStorage.DeletedFileNames);
        Assert.Contains(
            _auditLogs.AddedLogs,
            log => log.Action == "AttachmentDeleted");
    }

    [Fact]
    public async Task DeleteAsync_ShouldRejectAnotherOrdinaryProjectMember()
    {
        var task = CreateTask();
        AddMember(task.ProjectId, "another-member");

        var attachment = CreateAttachment(task.Id);

        var result = await CreateService().DeleteAsync(
            attachment.Id,
            "another-member");

        Assert.False(result);
        Assert.False(attachment.IsDeleted);
        Assert.Equal(0, _unitOfWork.SaveChangesCallCount);
        Assert.Empty(_fileStorage.DeletedFileNames);
    }

    [Fact]
    public async Task DeleteAsync_ShouldAllowProjectManagerToDeleteAnotherUsersAttachment()
    {
        var task = CreateTask();
        AddMember(
            task.ProjectId,
            "manager-123",
            ProjectMemberRole.Manager);

        var attachment = CreateAttachment(task.Id);

        var result = await CreateService().DeleteAsync(
            attachment.Id,
            "manager-123");

        Assert.True(result);
        Assert.True(attachment.IsDeleted);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
    }

    private sealed class FakeAttachmentRepository : ITaskAttachmentRepository
    {
        public List<TaskAttachment> AddedAttachments { get; } = [];

        public Task<TaskAttachment?> GetByIdAsync(
            Guid attachmentId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                AddedAttachments.FirstOrDefault(a =>
                    a.Id == attachmentId && !a.IsDeleted));

        public Task<IReadOnlyList<TaskAttachment>> GetByTaskIdAsync(
            Guid taskId,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<TaskAttachment> result = AddedAttachments
                .Where(a => a.TaskId == taskId && !a.IsDeleted)
                .ToList();

            return Task.FromResult(result);
        }

        public void Add(TaskAttachment attachment) =>
            AddedAttachments.Add(attachment);
    }

    private sealed class FakeTaskRepository : ITaskRepository
    {
        public List<TaskItem> AddedTasks { get; } = [];

        public Task<TaskItem?> GetByIdAsync(
            Guid taskId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                AddedTasks.FirstOrDefault(t =>
                    t.Id == taskId && !t.IsDeleted));

        public Task<IReadOnlyList<TaskItem>> GetByProjectIdAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<TaskItem> result = AddedTasks
                .Where(t => t.ProjectId == projectId && !t.IsDeleted)
                .ToList();

            return Task.FromResult(result);
        }

        public async Task<(IReadOnlyList<TaskItem> Items, int TotalCount)>
            GetPagedByProjectIdAsync(
                Guid projectId,
                SmartTask.BLL.DTOs.Tasks.TaskListQueryRequest query,
                CancellationToken cancellationToken = default)
        {
            var filtered = AddedTasks
                .Where(t => t.ProjectId == projectId && !t.IsDeleted)
                .ToList();

            var totalCount = filtered.Count;
            IReadOnlyList<TaskItem> items = filtered
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToList();

            return (items, totalCount);
        }

        public void SetOriginalRowVersion(TaskItem task, byte[] rowVersion) { }

        public void Add(TaskItem task) => AddedTasks.Add(task);

        public void Update(TaskItem task) { }
    }

    private sealed class FakeProjectMemberRepository : IProjectMemberRepository
    {
        public List<ProjectMember> AddedMembers { get; } = [];

        public Task<ProjectMember?> GetByProjectAndUserAsync(
            Guid projectId,
            string userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                AddedMembers.FirstOrDefault(m =>
                    m.ProjectId == projectId && m.UserId == userId));

        public Task<IReadOnlyList<ProjectMember>> GetByProjectIdAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ProjectMember> result = AddedMembers
                .Where(m => m.ProjectId == projectId)
                .ToList();

            return Task.FromResult(result);
        }

        public void Add(ProjectMember member) => AddedMembers.Add(member);

        public void Remove(ProjectMember member) => AddedMembers.Remove(member);
    }

    private sealed class FakeFileStorageService : IFileStorageService
    {
        public Dictionary<string, byte[]> SavedFiles { get; } = [];

        public List<string> DeletedFileNames { get; } = [];

        public int OpenReadCallCount { get; private set; }

        public Exception? DeleteException { get; set; }

        public async Task SaveAsync(
            Stream fileStream,
            string storedFileName,
            CancellationToken cancellationToken = default)
        {
            using var memoryStream = new MemoryStream();
            await fileStream.CopyToAsync(memoryStream, cancellationToken);
            SavedFiles.Add(storedFileName, memoryStream.ToArray());
        }

        public Task<Stream?> OpenReadAsync(
            string storedFileName,
            CancellationToken cancellationToken = default)
        {
            OpenReadCallCount++;

            if (!SavedFiles.TryGetValue(storedFileName, out var content))
            {
                return Task.FromResult<Stream?>(null);
            }

            return Task.FromResult<Stream?>(
                new MemoryStream(content, writable: false));
        }

        public Task DeleteAsync(
            string storedFileName,
            CancellationToken cancellationToken = default)
        {
            DeletedFileNames.Add(storedFileName);

            if (DeleteException is not null)
            {
                throw DeleteException;
            }

            SavedFiles.Remove(storedFileName);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAuditLogRepository : IAuditLogRepository
    {
        public List<AuditLog> AddedLogs { get; } = [];

        public Task<IReadOnlyList<AuditLog>> GetByTaskIdAsync(
            Guid taskId,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<AuditLog> result = AddedLogs
                .Where(a => a.EntityName == "Task" &&
                            a.EntityId == taskId.ToString())
                .ToList();

            return Task.FromResult(result);
        }

        public void Add(AuditLog auditLog) => AddedLogs.Add(auditLog);
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCallCount { get; private set; }

        public Exception? SaveChangesException { get; set; }

        public Task ExecuteInTransactionAsync(
            Func<CancellationToken, Task> operation,
            CancellationToken cancellationToken = default)
        {
            return operation(cancellationToken);
        }

        public Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            SaveChangesCallCount++;

            if (SaveChangesException is not null)
            {
                throw SaveChangesException;
            }

            return Task.FromResult(1);
        }
    }
}

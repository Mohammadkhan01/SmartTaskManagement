using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartTask.BLL.DTOs.TaskAttachments;
using SmartTask.DAL.Identity;
using SmartTask.DAL.Persistence;
using SmartTask.Domain.Entities;
using SmartTask.Domain.Enums;

namespace SmartTask.IntegrationTests;

public sealed class TaskAttachmentHttpIntegrationTests
{
    [SqlServerFact]
    public async Task AttachmentEndpoints_EnforceMembershipAndSupportFileLifecycle()
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                "SMARTTASK_TEST_CONNECTION_STRING")!;

        var connectionBuilder =
            new SqlConnectionStringBuilder(connectionString);

        Assert.Equal("SmartTaskTestDb", connectionBuilder.InitialCatalog);

        var storageRootPath = Path.Combine(
            Path.GetTempPath(),
            "SmartTaskAttachmentIntegrationTests",
            Guid.NewGuid().ToString("N"));

        await using var factory =
            new ProjectTestWebApplicationFactory(
                connectionString,
                storageRootPath);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });

        var options = new DbContextOptionsBuilder<SmartTaskDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        var managerId = Guid.NewGuid().ToString();
        var memberId = Guid.NewGuid().ToString();
        var outsiderId = Guid.NewGuid().ToString();
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();

        try
        {
            // Arrange: create users, project, memberships, and task.
            await using (var context = new SmartTaskDbContext(options))
            {
                context.Users.AddRange(
                    CreateUser(managerId),
                    CreateUser(memberId),
                    CreateUser(outsiderId));

                context.Projects.Add(new Project
                {
                    Id = projectId,
                    Name = "Attachment Integration Test Project",
                    CreatedByUserId = managerId
                });

                context.ProjectMembers.AddRange(
                    new ProjectMember
                    {
                        ProjectId = projectId,
                        UserId = managerId,
                        ProjectRole = ProjectMemberRole.Manager
                    },
                    new ProjectMember
                    {
                        ProjectId = projectId,
                        UserId = memberId,
                        ProjectRole = ProjectMemberRole.Member
                    });

                context.Tasks.Add(new TaskItem
                {
                    Id = taskId,
                    ProjectId = projectId,
                    Title = "Attachment Integration Test Task",
                    CreatedByUserId = managerId
                });

                await context.SaveChangesAsync();
            }

            var pdfBytes = System.Text.Encoding.ASCII.GetBytes(
                "%PDF-1.7\nAttachment integration test");

            // 1. A project member uploads a PDF.
            AuthenticateAs(client, memberId);

            using var form = new MultipartFormDataContent();
            using var fileContent = new ByteArrayContent(pdfBytes);

            fileContent.Headers.ContentType =
                new MediaTypeHeaderValue("application/pdf");

            form.Add(fileContent, "file", "report.pdf");

            var uploadResponse = await client.PostAsync(
                $"/api/tasks/{taskId}/attachments",
                form);

            Assert.Equal(
                HttpStatusCode.Created,
                uploadResponse.StatusCode);

            var uploadedAttachment =
                await uploadResponse.Content
                    .ReadFromJsonAsync<TaskAttachmentResponse>();

            Assert.NotNull(uploadedAttachment);
            Assert.Equal(taskId, uploadedAttachment.TaskId);
            Assert.Equal("report.pdf", uploadedAttachment.OriginalFileName);
            Assert.Equal("application/pdf", uploadedAttachment.ContentType);
            Assert.Equal(pdfBytes.Length, uploadedAttachment.FileSize);
            Assert.Equal(memberId, uploadedAttachment.UploadedByUserId);

            var attachmentId = uploadedAttachment.Id;

            // 2. A project member can list task attachments.
            var listResponse = await client.GetAsync(
                $"/api/tasks/{taskId}/attachments");

            Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

            var attachments =
                await listResponse.Content
                    .ReadFromJsonAsync<List<TaskAttachmentResponse>>();

            Assert.NotNull(attachments);
            Assert.Contains(
                attachments,
                attachment => attachment.Id == attachmentId);

            // 3. A project member can retrieve attachment metadata.
            var metadataResponse = await client.GetAsync(
                $"/api/task-attachments/{attachmentId}");

            Assert.Equal(HttpStatusCode.OK, metadataResponse.StatusCode);

            // 4. A project member can download the original file.
            var downloadResponse = await client.GetAsync(
                $"/api/task-attachments/{attachmentId}/download");

            Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);
            Assert.Equal(
                pdfBytes,
                await downloadResponse.Content.ReadAsByteArrayAsync());

            Assert.Equal(
                "attachment",
                downloadResponse.Content.Headers.ContentDisposition?.DispositionType);

            // 5. A non-member cannot access attachment endpoints.
            AuthenticateAs(client, outsiderId);

            var outsiderListResponse = await client.GetAsync(
                $"/api/tasks/{taskId}/attachments");

            Assert.Equal(
                HttpStatusCode.NotFound,
                outsiderListResponse.StatusCode);

            var outsiderMetadataResponse = await client.GetAsync(
                $"/api/task-attachments/{attachmentId}");

            Assert.Equal(
                HttpStatusCode.NotFound,
                outsiderMetadataResponse.StatusCode);

            var outsiderDownloadResponse = await client.GetAsync(
                $"/api/task-attachments/{attachmentId}/download");

            Assert.Equal(
                HttpStatusCode.NotFound,
                outsiderDownloadResponse.StatusCode);

            var outsiderDeleteResponse = await client.DeleteAsync(
                $"/api/task-attachments/{attachmentId}");

            Assert.Equal(
                HttpStatusCode.NotFound,
                outsiderDeleteResponse.StatusCode);

            // 6. The uploader can delete the attachment.
            AuthenticateAs(client, memberId);

            var deleteResponse = await client.DeleteAsync(
                $"/api/task-attachments/{attachmentId}");

            Assert.Equal(
                HttpStatusCode.NoContent,
                deleteResponse.StatusCode);

            // 7. A deleted attachment is no longer accessible.
            var deletedMetadataResponse = await client.GetAsync(
                $"/api/task-attachments/{attachmentId}");

            Assert.Equal(
                HttpStatusCode.NotFound,
                deletedMetadataResponse.StatusCode);

            var deletedDownloadResponse = await client.GetAsync(
                $"/api/task-attachments/{attachmentId}/download");

            Assert.Equal(
                HttpStatusCode.NotFound,
                deletedDownloadResponse.StatusCode);

            var finalListResponse = await client.GetAsync(
                $"/api/tasks/{taskId}/attachments");

            Assert.Equal(HttpStatusCode.OK, finalListResponse.StatusCode);

            var remainingAttachments =
                await finalListResponse.Content
                    .ReadFromJsonAsync<List<TaskAttachmentResponse>>();

            Assert.NotNull(remainingAttachments);
            Assert.DoesNotContain(
                remainingAttachments,
                attachment => attachment.Id == attachmentId);
        }
        finally
        {
            await CleanupAsync(
                options,
                projectId,
                taskId,
                managerId,
                memberId,
                outsiderId);

            if (Directory.Exists(storageRootPath))
            {
                Directory.Delete(storageRootPath, recursive: true);
            }
        }
    }

    private static ApplicationUser CreateUser(string userId)
    {
        var email = $"attachment-test-{Guid.NewGuid():N}@example.com";

        return new ApplicationUser
        {
            Id = userId,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString()
        };
    }

    private static void AuthenticateAs(HttpClient client, string userId)
    {
        client.DefaultRequestHeaders.Remove("X-Test-UserId");
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId);
    }

    private static async Task CleanupAsync(
        DbContextOptions<SmartTaskDbContext> options,
        Guid projectId,
        Guid taskId,
        params string[] userIds)
    {
        await using var context = new SmartTaskDbContext(options);

        var attachments = await context.TaskAttachments
            .IgnoreQueryFilters()
            .Where(attachment => attachment.TaskId == taskId)
            .ToListAsync();

        context.TaskAttachments.RemoveRange(attachments);

        var auditLogs = await context.AuditLogs
            .Where(log => log.UserId != null && userIds.Contains(log.UserId))
            .ToListAsync();

        context.AuditLogs.RemoveRange(auditLogs);

        var task = await context.Tasks
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.Id == taskId);

        if (task is not null)
        {
            context.Tasks.Remove(task);
        }

        var memberships = await context.ProjectMembers
            .IgnoreQueryFilters()
            .Where(member => member.ProjectId == projectId)
            .ToListAsync();

        context.ProjectMembers.RemoveRange(memberships);

        var project = await context.Projects
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.Id == projectId);

        if (project is not null)
        {
            context.Projects.Remove(project);
        }

        var users = await context.Users
            .Where(user => userIds.Contains(user.Id))
            .ToListAsync();

        context.Users.RemoveRange(users);

        await context.SaveChangesAsync();
    }
}

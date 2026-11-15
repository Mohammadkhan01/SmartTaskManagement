using System.Reflection;
using SmartTask.API.Controllers;
using SmartTask.BLL.Interfaces.Repositories;
using SmartTask.BLL.Interfaces.Services;
using SmartTask.BLL.Services.Tasks;
using SmartTask.DAL.Identity;
using SmartTask.DAL.Persistence.Repositories;
using SmartTask.DAL.Storage;
using SmartTask.Domain;

namespace SmartTask.ArchitectureTests;

public sealed class LayerDependencyTests
{
    private static string[] GetReferencedProjectNames(Assembly assembly)
    {
        return assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(name => name.StartsWith("SmartTask."))
            .ToArray();
    }

    [Fact]
    public void Domain_MustNotDependOnApplicationLayers()
    {
        var references = GetReferencedProjectNames(
            typeof(SmartTask.Domain.Entities.Project).Assembly);

        Assert.DoesNotContain("SmartTask.BLL", references);
        Assert.DoesNotContain("SmartTask.DAL", references);
        Assert.DoesNotContain("SmartTask.API", references);
    }

    [Fact]
    public void BLL_MustNotDependOnDALOrAPI()
    {
        var references = GetReferencedProjectNames(
            typeof(TaskService).Assembly);

        Assert.DoesNotContain("SmartTask.DAL", references);
        Assert.DoesNotContain("SmartTask.API", references);
    }

    [Fact]
    public void DAL_MustNotDependOnAPI()
    {
        var references = GetReferencedProjectNames(
            typeof(ProjectRepository).Assembly);

        Assert.DoesNotContain("SmartTask.API", references);
    }

    [Fact]
    public void API_MustReferenceBLLAndDAL_AsCompositionRoot()
    {
        var references = GetReferencedProjectNames(
            typeof(TasksController).Assembly);

        Assert.Contains("SmartTask.BLL", references);
        Assert.Contains("SmartTask.DAL", references);
    }

    [Fact]
    public void RepositoryAbstractions_MustBeDefinedInBLL()
    {
        var bllAssembly = typeof(TaskService).Assembly;

        Assert.Equal(bllAssembly, typeof(IProjectRepository).Assembly);
        Assert.Equal(bllAssembly, typeof(IProjectMemberRepository).Assembly);
        Assert.Equal(bllAssembly, typeof(ITaskRepository).Assembly);
        Assert.Equal(bllAssembly, typeof(ITaskCommentRepository).Assembly);
        Assert.Equal(bllAssembly, typeof(ITaskAttachmentRepository).Assembly);
        Assert.Equal(bllAssembly, typeof(IUserAccountRepository).Assembly);
        Assert.Equal(bllAssembly, typeof(IAuditLogRepository).Assembly);
        Assert.Equal(bllAssembly, typeof(IUnitOfWork).Assembly);
    }

    [Fact]
    public void DAL_Implementations_MustImplementBLLAbstractions()
    {
        Assert.True(
            typeof(IProjectRepository)
                .IsAssignableFrom(typeof(ProjectRepository)));

        Assert.True(
            typeof(IProjectMemberRepository)
                .IsAssignableFrom(typeof(ProjectMemberRepository)));

        Assert.True(
            typeof(ITaskRepository)
                .IsAssignableFrom(typeof(TaskRepository)));

        Assert.True(
            typeof(ITaskCommentRepository)
                .IsAssignableFrom(typeof(TaskCommentRepository)));

        Assert.True(
            typeof(ITaskAttachmentRepository)
                .IsAssignableFrom(typeof(TaskAttachmentRepository)));

        Assert.True(
            typeof(IUserAccountRepository)
                .IsAssignableFrom(typeof(UserAccountRepository)));

        Assert.True(
            typeof(IAuditLogRepository)
                .IsAssignableFrom(typeof(AuditLogRepository)));

        Assert.True(
            typeof(IFileStorageService)
                .IsAssignableFrom(typeof(LocalFileStorageService)));
    }
}

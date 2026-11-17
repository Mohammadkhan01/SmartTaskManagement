using Moq;
using SmartTask.BLL.DTOs.Dashboard;
using SmartTask.BLL.Interfaces.Repositories;
using SmartTask.BLL.Services.Dashboard;
using SmartTask.Domain.Entities;
using SmartTask.Domain.Enums;

namespace SmartTask.UnitTests;

public class DashboardServiceTests
{
    private readonly Mock<IDashboardRepository> _dashboardRepository = new();
    private readonly Mock<IProjectMemberRepository> _memberRepository = new();

    private DashboardService CreateService() =>
        new(
            _dashboardRepository.Object,
            _memberRepository.Object);

    [Fact]
    public async Task GetMyDashboardAsync_ShouldUseAuthenticatedUserId()
    {
        var expected = new DashboardSummaryResponse
        {
            TotalTasks = 5,
            CompletedTasks = 2
        };

        _dashboardRepository
            .Setup(repository => repository.GetMyDashboardAsync(
                "user-123",
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                10,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await CreateService().GetMyDashboardAsync("user-123");

        Assert.Same(expected, result);

        _dashboardRepository.Verify(repository =>
            repository.GetMyDashboardAsync(
                "user-123",
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                10,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetMyDashboardAsync_ShouldRejectEmptyUserId()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateService().GetMyDashboardAsync(" "));

        _dashboardRepository.Verify(repository =>
            repository.GetMyDashboardAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetProjectDashboardAsync_ShouldAllowProjectMember()
    {
        var projectId = Guid.NewGuid();

        _memberRepository
            .Setup(repository => repository.GetByProjectAndUserAsync(
                projectId,
                "member-123",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProjectMember
            {
                ProjectId = projectId,
                UserId = "member-123",
                ProjectRole = ProjectMemberRole.Member
            });

        var expected = new DashboardSummaryResponse
        {
            ProjectId = projectId,
            ProjectName = "Test Project"
        };

        _dashboardRepository
            .Setup(repository => repository.GetProjectDashboardAsync(
                projectId,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                10,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await CreateService().GetProjectDashboardAsync(
            projectId,
            "member-123");

        Assert.Same(expected, result);

        _dashboardRepository.Verify(repository =>
            repository.GetProjectDashboardAsync(
                projectId,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                10,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetProjectDashboardAsync_ShouldRejectNonMember()
    {
        var projectId = Guid.NewGuid();

        _memberRepository
            .Setup(repository => repository.GetByProjectAndUserAsync(
                projectId,
                "outsider-123",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProjectMember?)null);

        var result = await CreateService().GetProjectDashboardAsync(
            projectId,
            "outsider-123");

        Assert.Null(result);

        _dashboardRepository.Verify(repository =>
            repository.GetProjectDashboardAsync(
                It.IsAny<Guid>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetProjectDashboardAsync_ShouldRejectEmptyProjectId()
    {
        var result = await CreateService().GetProjectDashboardAsync(
            Guid.Empty,
            "user-123");

        Assert.Null(result);

        _memberRepository.Verify(repository =>
            repository.GetByProjectAndUserAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetProjectWorkloadAsync_ShouldAllowManager()
    {
        var projectId = Guid.NewGuid();

        _memberRepository
            .Setup(repository => repository.GetByProjectAndUserAsync(
                projectId,
                "manager-123",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProjectMember
            {
                ProjectId = projectId,
                UserId = "manager-123",
                ProjectRole = ProjectMemberRole.Manager
            });

        IReadOnlyList<MemberWorkloadResponse> expected =
        [
            new()
            {
                UserId = "member-123",
                DisplayName = "Test Member",
                TotalTasks = 4
            }
        ];

        _dashboardRepository
            .Setup(repository => repository.GetProjectWorkloadAsync(
                projectId,
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await CreateService().GetProjectWorkloadAsync(
            projectId,
            "manager-123");

        Assert.Same(expected, result);

        _dashboardRepository.Verify(repository =>
            repository.GetProjectWorkloadAsync(
                projectId,
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetProjectWorkloadAsync_ShouldRejectRegularMember()
    {
        var projectId = Guid.NewGuid();

        _memberRepository
            .Setup(repository => repository.GetByProjectAndUserAsync(
                projectId,
                "member-123",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProjectMember
            {
                ProjectId = projectId,
                UserId = "member-123",
                ProjectRole = ProjectMemberRole.Member
            });

        var result = await CreateService().GetProjectWorkloadAsync(
            projectId,
            "member-123");

        Assert.Null(result);

        _dashboardRepository.Verify(repository =>
            repository.GetProjectWorkloadAsync(
                It.IsAny<Guid>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetProjectWorkloadAsync_ShouldRejectNonMember()
    {
        var projectId = Guid.NewGuid();

        _memberRepository
            .Setup(repository => repository.GetByProjectAndUserAsync(
                projectId,
                "outsider-123",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProjectMember?)null);

        var result = await CreateService().GetProjectWorkloadAsync(
            projectId,
            "outsider-123");

        Assert.Null(result);

        _dashboardRepository.Verify(repository =>
            repository.GetProjectWorkloadAsync(
                It.IsAny<Guid>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetMyTaskStatisticsAsync_ShouldUseAuthenticatedUserId()
    {
        var expected = new DashboardTaskStatisticsResponse
        {
            TotalTasks = 6,
            CompletedTasks = 2,
            PendingTasks = 3,
            CompletionPercentage = 33.33m
        };

        _dashboardRepository
            .Setup(repository => repository.GetMyTaskStatisticsAsync(
                "user-123",
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                10,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await CreateService().GetMyTaskStatisticsAsync("user-123");

        Assert.Same(expected, result);

        _dashboardRepository.Verify(repository =>
            repository.GetMyTaskStatisticsAsync(
                "user-123",
                It.IsAny<DateTime>(),
                It.Is<DateTime>(cutoff => cutoff > DateTime.UtcNow),
                10,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetMyTaskStatisticsAsync_ShouldRejectEmptyUserId()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateService().GetMyTaskStatisticsAsync(" "));

        _dashboardRepository.Verify(repository =>
            repository.GetMyTaskStatisticsAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

}

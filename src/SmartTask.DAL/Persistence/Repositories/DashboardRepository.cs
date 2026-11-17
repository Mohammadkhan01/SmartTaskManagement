using Microsoft.EntityFrameworkCore;
using SmartTask.BLL.DTOs.Dashboard;
using SmartTask.BLL.Interfaces.Repositories;
using SmartTask.DAL.Persistence;
using SmartTask.Domain.Entities;
using SmartTask.Domain.Enums;
using TaskStatus = SmartTask.Domain.Enums.TaskStatus;

namespace SmartTask.DAL.Persistence.Repositories;

public sealed class DashboardRepository : IDashboardRepository
{
    private readonly SmartTaskDbContext _context;

    public DashboardRepository(SmartTaskDbContext context)
    {
        _context = context;
    }

    public async Task<DashboardTaskStatisticsResponse> GetMyTaskStatisticsAsync(
        string userId,
        DateTime nowUtc,
        DateTime upcomingCutoffUtc,
        int upcomingTaskCount,
        CancellationToken cancellationToken = default)
    {
        var tasks = _context.Tasks
            .AsNoTracking()
            .Where(t => t.AssignedToUserId == userId);

        var aggregate = await tasks
            .GroupBy(_ => 1)
            .Select(group => new
            {
                TotalTasks = group.Count(),
                ToDoTasks = group.Count(t => t.Status == TaskStatus.ToDo),
                InProgressTasks = group.Count(t => t.Status == TaskStatus.InProgress),
                CompletedTasks = group.Count(t => t.Status == TaskStatus.Completed),
                CancelledTasks = group.Count(t => t.Status == TaskStatus.Cancelled),
                LowPriorityTasks = group.Count(t => t.Priority == TaskPriority.Low),
                MediumPriorityTasks = group.Count(t => t.Priority == TaskPriority.Medium),
                HighPriorityTasks = group.Count(t => t.Priority == TaskPriority.High),
                CriticalPriorityTasks = group.Count(t => t.Priority == TaskPriority.Critical)
            })
            .SingleOrDefaultAsync(cancellationToken);

        var upcomingTasks = await tasks
            .Where(t =>
                t.DueDateUtc.HasValue &&
                t.DueDateUtc.Value > nowUtc &&
                t.DueDateUtc.Value <= upcomingCutoffUtc &&
                t.Status != TaskStatus.Completed &&
                t.Status != TaskStatus.Cancelled)
            .OrderBy(t => t.DueDateUtc)
            .ThenBy(t => t.Id)
            .Take(upcomingTaskCount)
            .Select(t => new DashboardTaskItemResponse
            {
                Id = t.Id,
                ProjectId = t.ProjectId,
                Title = t.Title,
                Status = t.Status,
                Priority = t.Priority,
                DueDateUtc = t.DueDateUtc,
                CreatedAtUtc = t.CreatedAtUtc,
                UpdatedAtUtc = t.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);

        var totalTasks = aggregate?.TotalTasks ?? 0;
        var completedTasks = aggregate?.CompletedTasks ?? 0;
        var toDoTasks = aggregate?.ToDoTasks ?? 0;
        var inProgressTasks = aggregate?.InProgressTasks ?? 0;

        return new DashboardTaskStatisticsResponse
        {
            TotalTasks = totalTasks,
            ToDoTasks = toDoTasks,
            InProgressTasks = inProgressTasks,
            CompletedTasks = completedTasks,
            CancelledTasks = aggregate?.CancelledTasks ?? 0,
            PendingTasks = toDoTasks + inProgressTasks,
            CompletionPercentage = totalTasks == 0
                ? 0
                : Math.Round(completedTasks * 100m / totalTasks, 2),
            LowPriorityTasks = aggregate?.LowPriorityTasks ?? 0,
            MediumPriorityTasks = aggregate?.MediumPriorityTasks ?? 0,
            HighPriorityTasks = aggregate?.HighPriorityTasks ?? 0,
            CriticalPriorityTasks = aggregate?.CriticalPriorityTasks ?? 0,
            UpcomingTasks = upcomingTasks
        };
    }

    public async Task<DashboardSummaryResponse> GetMyDashboardAsync(
        string userId,
        DateTime nowUtc,
        DateTime startOfTodayUtc,
        DateTime startOfTomorrowUtc,
        int recentTaskCount,
        CancellationToken cancellationToken = default)
    {
        var tasks = _context.Tasks
            .AsNoTracking()
            .Where(t => t.AssignedToUserId == userId);

        return await BuildSummaryAsync(
            tasks,
            null,
            null,
            nowUtc,
            startOfTodayUtc,
            startOfTomorrowUtc,
            recentTaskCount,
            cancellationToken);
    }

    public async Task<DashboardSummaryResponse> GetProjectDashboardAsync(
        Guid projectId,
        DateTime nowUtc,
        DateTime startOfTodayUtc,
        DateTime startOfTomorrowUtc,
        int recentTaskCount,
        CancellationToken cancellationToken = default)
    {
        var projectName = await _context.Projects
            .AsNoTracking()
            .Where(p => p.Id == projectId)
            .Select(p => p.Name)
            .SingleAsync(cancellationToken);

        var tasks = _context.Tasks
            .AsNoTracking()
            .Where(t => t.ProjectId == projectId);

        return await BuildSummaryAsync(
            tasks,
            projectId,
            projectName,
            nowUtc,
            startOfTodayUtc,
            startOfTomorrowUtc,
            recentTaskCount,
            cancellationToken);
    }

    public async Task<IReadOnlyList<MemberWorkloadResponse>>
        GetProjectWorkloadAsync(
            Guid projectId,
            DateTime nowUtc,
            CancellationToken cancellationToken = default)
    {
        var members = await (
            from membership in _context.ProjectMembers.AsNoTracking()
            join user in _context.Users.AsNoTracking()
                on membership.UserId equals user.Id
            where membership.ProjectId == projectId
            select new
            {
                membership.UserId,
                DisplayName = user.DisplayName ?? user.Email ?? user.Id
            })
            .ToListAsync(cancellationToken);

        var taskAggregates = await _context.Tasks
            .AsNoTracking()
            .Where(t =>
                t.ProjectId == projectId &&
                t.AssignedToUserId != null)
            .GroupBy(t => t.AssignedToUserId!)
            .Select(group => new
            {
                UserId = group.Key,
                TotalTasks = group.Count(),
                ToDoTasks = group.Count(t => t.Status == TaskStatus.ToDo),
                InProgressTasks = group.Count(
                    t => t.Status == TaskStatus.InProgress),
                CompletedTasks = group.Count(
                    t => t.Status == TaskStatus.Completed),
                OverdueTasks = group.Count(t =>
                    t.DueDateUtc.HasValue &&
                    t.DueDateUtc.Value < nowUtc &&
                    t.Status != TaskStatus.Completed &&
                    t.Status != TaskStatus.Cancelled)
            })
            .ToListAsync(cancellationToken);

        var aggregatesByUser = taskAggregates.ToDictionary(
            item => item.UserId,
            item => item);

        return members
            .Select(member =>
            {
                aggregatesByUser.TryGetValue(
                    member.UserId,
                    out var aggregate);

                return new MemberWorkloadResponse
                {
                    UserId = member.UserId,
                    DisplayName = member.DisplayName,
                    TotalTasks = aggregate?.TotalTasks ?? 0,
                    ToDoTasks = aggregate?.ToDoTasks ?? 0,
                    InProgressTasks = aggregate?.InProgressTasks ?? 0,
                    CompletedTasks = aggregate?.CompletedTasks ?? 0,
                    OverdueTasks = aggregate?.OverdueTasks ?? 0
                };
            })
            .OrderByDescending(item => item.TotalTasks)
            .ThenBy(item => item.DisplayName)
            .ToList();
    }

    private async Task<DashboardSummaryResponse> BuildSummaryAsync(
        IQueryable<TaskItem> tasks,
        Guid? projectId,
        string? projectName,
        DateTime nowUtc,
        DateTime startOfTodayUtc,
        DateTime startOfTomorrowUtc,
        int recentTaskCount,
        CancellationToken cancellationToken)
    {
        var aggregate = await tasks
            .GroupBy(_ => 1)
            .Select(group => new DashboardAggregate
            {
                TotalTasks = group.Count(),
                ToDoTasks = group.Count(
                    t => t.Status == TaskStatus.ToDo),
                InProgressTasks = group.Count(
                    t => t.Status == TaskStatus.InProgress),
                CompletedTasks = group.Count(
                    t => t.Status == TaskStatus.Completed),
                CancelledTasks = group.Count(
                    t => t.Status == TaskStatus.Cancelled),
                OverdueTasks = group.Count(t =>
                    t.DueDateUtc.HasValue &&
                    t.DueDateUtc.Value < nowUtc &&
                    t.Status != TaskStatus.Completed &&
                    t.Status != TaskStatus.Cancelled),
                DueTodayTasks = group.Count(t =>
                    t.DueDateUtc.HasValue &&
                    t.DueDateUtc.Value >= startOfTodayUtc &&
                    t.DueDateUtc.Value < startOfTomorrowUtc &&
                    t.Status != TaskStatus.Completed &&
                    t.Status != TaskStatus.Cancelled)
            })
            .SingleOrDefaultAsync(cancellationToken);

        var recentTasks = await tasks
            .OrderByDescending(t => t.UpdatedAtUtc ?? t.CreatedAtUtc)
            .ThenByDescending(t => t.Id)
            .Take(recentTaskCount)
            .Select(t => new DashboardTaskItemResponse
            {
                Id = t.Id,
                ProjectId = t.ProjectId,
                Title = t.Title,
                Status = t.Status,
                Priority = t.Priority,
                DueDateUtc = t.DueDateUtc,
                CreatedAtUtc = t.CreatedAtUtc,
                UpdatedAtUtc = t.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);

        var totalTasks = aggregate?.TotalTasks ?? 0;
        var completedTasks = aggregate?.CompletedTasks ?? 0;

        return new DashboardSummaryResponse
        {
            ProjectId = projectId,
            ProjectName = projectName,
            TotalTasks = totalTasks,
            ToDoTasks = aggregate?.ToDoTasks ?? 0,
            InProgressTasks = aggregate?.InProgressTasks ?? 0,
            CompletedTasks = completedTasks,
            CancelledTasks = aggregate?.CancelledTasks ?? 0,
            OverdueTasks = aggregate?.OverdueTasks ?? 0,
            DueTodayTasks = aggregate?.DueTodayTasks ?? 0,
            CompletionPercentage = totalTasks == 0
                ? 0
                : Math.Round(
                    completedTasks * 100m / totalTasks,
                    2),
            RecentTasks = recentTasks
        };
    }

    private sealed class DashboardAggregate
    {
        public int TotalTasks { get; set; }
        public int ToDoTasks { get; set; }
        public int InProgressTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int CancelledTasks { get; set; }
        public int OverdueTasks { get; set; }
        public int DueTodayTasks { get; set; }
    }
}

using SmartTask.BLL.DTOs.Tasks;
using SmartTask.BLL.Validators;
using SmartTask.Domain.Enums;
using TaskStatus = SmartTask.Domain.Enums.TaskStatus;

namespace SmartTask.UnitTests;

public class TaskListQueryRequestValidatorTests
{
    private readonly TaskListQueryRequestValidator _validator = new();

    [Fact]
    public void Validate_ShouldAcceptValidQuery()
    {
        var request = new TaskListQueryRequest
        {
            PageNumber = 1,
            PageSize = 20,
            Search = "API",
            Status = TaskStatus.InProgress,
            Priority = TaskPriority.High,
            SortBy = "createdAt",
            SortDescending = true
        };

        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void Validate_ShouldRejectInvalidPagination(
        int pageNumber,
        int pageSize)
    {
        var result = _validator.Validate(new TaskListQueryRequest
        {
            PageNumber = pageNumber,
            PageSize = pageSize
        });

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("unknownField")]
    public void Validate_ShouldRejectUnsupportedSortField(string sortBy)
    {
        var result = _validator.Validate(new TaskListQueryRequest
        {
            SortBy = sortBy
        });

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == nameof(TaskListQueryRequest.SortBy));
    }

    [Fact]
    public void Validate_ShouldRejectReversedDueDateRange()
    {
        var result = _validator.Validate(new TaskListQueryRequest
        {
            DueDateFromUtc = new DateTime(2026, 11, 15),
            DueDateToUtc = new DateTime(2026, 11, 14)
        });

        Assert.False(result.IsValid);
    }
}

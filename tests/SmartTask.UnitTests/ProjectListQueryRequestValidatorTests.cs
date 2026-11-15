using SmartTask.BLL.DTOs.Projects;
using SmartTask.BLL.Validators;
using SmartTask.Domain.Enums;

namespace SmartTask.UnitTests;

public class ProjectListQueryRequestValidatorTests
{
    private readonly ProjectListQueryRequestValidator _validator = new();

    [Fact]
    public void Validate_ShouldAcceptValidQuery()
    {
        var result = _validator.Validate(new ProjectListQueryRequest
        {
            PageNumber = 1,
            PageSize = 20,
            Search = "API",
            Status = ProjectStatus.Active,
            SortBy = "name",
            SortDescending = false
        });

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
        var result = _validator.Validate(new ProjectListQueryRequest
        {
            PageNumber = pageNumber,
            PageSize = pageSize
        });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_ShouldRejectPaginationOffsetOverflow()
    {
        var result = _validator.Validate(new ProjectListQueryRequest
        {
            PageNumber = int.MaxValue,
            PageSize = 100
        });

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("unknownField")]
    public void Validate_ShouldRejectUnsupportedSortField(string sortBy)
    {
        var result = _validator.Validate(new ProjectListQueryRequest
        {
            SortBy = sortBy
        });

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.PropertyName ==
                nameof(ProjectListQueryRequest.SortBy));
    }

    [Fact]
    public void Validate_ShouldRejectUndefinedStatus()
    {
        var result = _validator.Validate(new ProjectListQueryRequest
        {
            Status = (ProjectStatus)999
        });

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.PropertyName ==
                nameof(ProjectListQueryRequest.Status));
    }

    [Fact]
    public void Validate_ShouldRejectSearchLongerThan200Characters()
    {
        var result = _validator.Validate(new ProjectListQueryRequest
        {
            Search = new string('a', 201)
        });

        Assert.False(result.IsValid);
    }
}

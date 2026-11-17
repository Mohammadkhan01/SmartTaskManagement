using SmartTask.BLL.DTOs.AI;
using SmartTask.BLL.DTOs.Tasks;
using SmartTask.BLL.Validators;
using SmartTask.Common.Constants;
using SmartTask.Domain.Enums;

namespace SmartTask.UnitTests;

public class DescriptionLengthValidatorTests
{
    private const string ValidRowVersion = "AQIDBAUGBwg=";

    [Fact]
    public void TaskDescriptionLimit_ShouldMatchHandbookColumnLength()
    {
        Assert.Equal(4000, FieldLimits.TaskDescriptionMaxLength);
    }

    [Theory]
    [InlineData(4000, true)]
    [InlineData(4001, false)]
    public void CreateTaskRequest_ShouldEnforceDescriptionBoundary(
        int length,
        bool expectedValid)
    {
        var request = new CreateTaskRequest
        {
            Title = "Boundary task",
            Description = new string('d', length),
            Priority = TaskPriority.Medium
        };

        var result = new CreateTaskRequestValidator().Validate(request);

        Assert.Equal(expectedValid, result.IsValid);
        AssertDescriptionError(result, expectedValid);
    }

    [Theory]
    [InlineData(4000, true)]
    [InlineData(4001, false)]
    public void UpdateTaskRequest_ShouldEnforceDescriptionBoundary(
        int length,
        bool expectedValid)
    {
        var request = new UpdateTaskRequest
        {
            Title = "Boundary task",
            Description = new string('d', length),
            Priority = TaskPriority.Medium,
            RowVersion = ValidRowVersion
        };

        var result = new UpdateTaskRequestValidator().Validate(request);

        Assert.Equal(expectedValid, result.IsValid);
        AssertDescriptionError(result, expectedValid);
    }

    [Theory]
    [InlineData(4000, true)]
    [InlineData(4001, false)]
    public void EnhanceTaskDescriptionRequest_ShouldEnforceDescriptionBoundary(
        int length,
        bool expectedValid)
    {
        var request = new EnhanceTaskDescriptionRequest
        {
            Title = "Boundary task",
            Description = new string('d', length)
        };

        var result = new EnhanceTaskDescriptionRequestValidator().Validate(request);

        Assert.Equal(expectedValid, result.IsValid);
        AssertDescriptionError(result, expectedValid);
    }

    private static void AssertDescriptionError(
        FluentValidation.Results.ValidationResult result,
        bool expectedValid)
    {
        if (expectedValid)
            return;

        var error = Assert.Single(result.Errors);
        Assert.Equal("Description", error.PropertyName);
        Assert.Contains("4000", error.ErrorMessage);
    }
}

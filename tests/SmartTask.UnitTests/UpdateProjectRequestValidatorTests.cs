using SmartTask.BLL.DTOs.Projects;
using SmartTask.BLL.Validators;

namespace SmartTask.UnitTests;

public class UpdateProjectRequestValidatorTests
{
    private readonly UpdateProjectRequestValidator _validator = new();

    [Fact]
    public void Validate_ShouldAcceptValidEightByteBase64RowVersion()
    {
        var request = new UpdateProjectRequest
        {
            Name = "Valid project",
            RowVersion = Convert.ToBase64String(
                [1, 2, 3, 4, 5, 6, 7, 8])
        };

        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-base64")]
    [InlineData("AQID")] 
    [InlineData("AQIDBAUGBwgJCg==")]
    public void Validate_ShouldRejectInvalidRowVersion(string rowVersion)
    {
        var request = new UpdateProjectRequest
        {
            Name = "Valid project",
            RowVersion = rowVersion
        };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == nameof(UpdateProjectRequest.RowVersion));
    }
}

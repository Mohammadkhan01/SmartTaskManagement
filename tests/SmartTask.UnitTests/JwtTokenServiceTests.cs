using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using SmartTask.API.Authentication;

namespace SmartTask.UnitTests;

public class JwtTokenServiceTests
{
    private const string TestSigningKey =
        "ThisIsATestSigningKeyWithAtLeast32Bytes!";

    private static JwtTokenService CreateService()
    {
        var settings = new JwtSettings
        {
            Issuer = "SmartTask.API",
            Audience = "SmartTask.Client",
            SigningKey = TestSigningKey,
            AccessTokenExpirationMinutes = 15
        };

        return new JwtTokenService(Options.Create(settings));
    }

    [Fact]
    public void GenerateAccessToken_ShouldReturnValidToken()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = service.GenerateAccessToken(
            "user-123",
            "user@example.com",
            ["Admin", "Project Manager"]);

        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(result.AccessToken);

        // Assert
        Assert.NotNull(result.AccessToken);
        Assert.Equal("SmartTask.API", token.Issuer);
        Assert.Contains("SmartTask.Client", token.Audiences);

        Assert.Equal(
            "user-123",
            token.Claims.First(c =>
                c.Type == JwtRegisteredClaimNames.Sub).Value);

        Assert.Equal(
            "user@example.com",
            token.Claims.First(c =>
                c.Type == JwtRegisteredClaimNames.Email).Value);

        Assert.Contains(
            token.Claims,
            c => c.Type == ClaimTypes.Role && c.Value == "Admin");

        Assert.Contains(
            token.Claims,
            c => c.Type == ClaimTypes.Role &&
                 c.Value == "Project Manager");
    }

    [Fact]
    public void GenerateAccessToken_ShouldSetExpirationTo15Minutes()
    {
        // Arrange
        var service = CreateService();
        var beforeGeneration = DateTime.UtcNow;

        // Act
        var result = service.GenerateAccessToken(
            "user-123",
            "user@example.com",
            []);

        // Assert
        Assert.True(result.ExpiresAtUtc >=
            beforeGeneration.AddMinutes(14));

        Assert.True(result.ExpiresAtUtc <=
            beforeGeneration.AddMinutes(16));
    }

    [Fact]
    public void GenerateAccessToken_ShouldRejectEmptyUserId()
    {
        // Arrange
        var service = CreateService();

        // Act and Assert
        Assert.Throws<ArgumentException>(() =>
            service.GenerateAccessToken(
                "",
                "user@example.com",
                []));
    }

    [Fact]
    public void GenerateAccessToken_ShouldRejectEmptyEmail()
    {
        // Arrange
        var service = CreateService();

        // Act and Assert
        Assert.Throws<ArgumentException>(() =>
            service.GenerateAccessToken(
                "user-123",
                "",
                []));
    }
}

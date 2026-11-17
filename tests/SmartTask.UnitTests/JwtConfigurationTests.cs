using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartTask.API.Authentication;
using SmartTask.BLL.Interfaces.Services;

namespace SmartTask.UnitTests;

public sealed class JwtConfigurationTests
{
    private const string TestSigningKey =
        "UnitTestSigningKeyWithAtLeast32Bytes!";

    private static IConfiguration CreateConfiguration(
        Dictionary<string, string?>? overrides = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "SmartTask.UnitTests",
            ["Jwt:Audience"] = "SmartTask.UnitTests.Client",
            ["Jwt:SigningKey"] = TestSigningKey,
            ["Jwt:AccessTokenExpirationMinutes"] = "15"
        };

        if (overrides is not null)
        {
            foreach (var item in overrides)
            {
                settings[item.Key] = item.Value;
            }
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
    }

    [Fact]
    public void AddJwtAuthentication_WhenJwtSectionIsMissing_Throws()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>())
            .Build();

        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddJwtAuthentication(configuration));

        Assert.Equal(
            "JWT configuration section is missing.",
            exception.Message);
    }

    [Theory]
    [InlineData("Jwt:Issuer")]
    [InlineData("Jwt:Audience")]
    [InlineData("Jwt:SigningKey")]
    public void AddJwtAuthentication_WhenRequiredSettingIsMissing_Throws(
        string setting)
    {
        var configuration = CreateConfiguration(
            new Dictionary<string, string?>
            {
                [setting] = ""
            });

        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(
            () => services.AddJwtAuthentication(configuration));
    }

    [Fact]
    public void AddJwtAuthentication_WhenSigningKeyIsTooShort_Throws()
    {
        var configuration = CreateConfiguration(
            new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"] = "short-test-key"
            });

        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddJwtAuthentication(configuration));

        Assert.Equal(
            "JWT signing key must be at least 32 bytes.",
            exception.Message);
    }

    [Fact]
    public void AddJwtAuthentication_WhenAccessTokenExpiryIsNotPositive_Throws()
    {
        var configuration = CreateConfiguration(
            new Dictionary<string, string?>
            {
                ["Jwt:AccessTokenExpirationMinutes"] = "0"
            });

        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddJwtAuthentication(configuration));

        Assert.Equal(
            "JWT access token expiration must be greater than zero.",
            exception.Message);
    }

    [Fact]
    public void AddJwtAuthentication_WhenConfigurationIsValid_RegistersJwtServices()
    {
        var configuration = CreateConfiguration();
        var services = new ServiceCollection();

        services.AddJwtAuthentication(configuration);

        Assert.Contains(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(IJwtTokenService));
    }
}

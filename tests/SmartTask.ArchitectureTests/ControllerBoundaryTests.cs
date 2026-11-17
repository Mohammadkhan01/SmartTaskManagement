using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Extensions.Options;
using SmartTask.API.Authentication;
using SmartTask.API.Controllers;

namespace SmartTask.ArchitectureTests;

public sealed class ControllerBoundaryTests
{
    [Fact]
    public void BusinessControllers_MustDependOnlyOnBllServiceInterfaces()
    {
        var controllers = typeof(TasksController)
            .Assembly
            .GetTypes()
            .Where(type =>
                type.IsClass &&
                !type.IsAbstract &&
                type.Namespace == typeof(TasksController).Namespace &&
                typeof(ControllerBase).IsAssignableFrom(type))
            .ToArray();

        Assert.NotEmpty(controllers);

        foreach (var controller in controllers)
        {
            var constructors = controller.GetConstructors();

            Assert.NotEmpty(constructors);

            foreach (var constructor in constructors)
            {
                foreach (var parameter in constructor.GetParameters())
                {
                    // AuthController requires framework services for CSRF
                    // protection and strongly typed cookie configuration.
                    if (parameter.ParameterType == typeof(IAntiforgery) ||
                        parameter.ParameterType ==
                            typeof(IOptions<RefreshTokenCookieSettings>))
                    {
                        continue;
                    }

                    Assert.True(
                        parameter.ParameterType.IsInterface,
                        $"{controller.Name} must not depend on concrete type " +
                        $"{parameter.ParameterType.FullName}.");

                    Assert.Equal(
                        "SmartTask.BLL.Interfaces.Services",
                        parameter.ParameterType.Namespace);
                }
            }
        }
    }
}

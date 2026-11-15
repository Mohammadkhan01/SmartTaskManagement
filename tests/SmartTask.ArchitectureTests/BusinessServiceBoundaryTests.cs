using SmartTask.BLL.Services.Tasks;
using SmartTask.BLL.Services;

namespace SmartTask.ArchitectureTests;

public sealed class BusinessServiceBoundaryTests
{
    [Fact]
    public void BusinessServices_MustDependOnAbstractions()
    {
        var serviceTypes = typeof(TaskService)
            .Assembly
            .GetTypes()
            .Where(type =>
                type.IsClass &&
                !type.IsAbstract &&
                type.Namespace is not null &&
                type.Namespace.StartsWith(
                    "SmartTask.BLL.Services",
                    StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(serviceTypes);

        foreach (var serviceType in serviceTypes)
        {
            var constructors = serviceType.GetConstructors();

            Assert.NotEmpty(constructors);

            foreach (var constructor in constructors)
            {
                foreach (var parameter in constructor.GetParameters())
                {
                    var dependencyType = parameter.ParameterType;

                    Assert.True(
                        dependencyType.IsInterface,
                        $"{serviceType.Name} must depend on an interface, " +
                        $"not {dependencyType.FullName}.");

                    if (dependencyType.Namespace?.StartsWith(
                            "SmartTask.",
                            StringComparison.Ordinal) == true)
                    {
                        Assert.StartsWith(
                            "SmartTask.BLL.Interfaces.",
                            dependencyType.Namespace);
                    }
                }
            }
        }
    }
}

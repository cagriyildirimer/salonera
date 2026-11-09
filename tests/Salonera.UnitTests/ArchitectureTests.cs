using System.Reflection;

namespace Salonera.UnitTests;

public sealed class ArchitectureTests
{
    [Fact]
    public void DomainDoesNotReferenceApplicationOrInfrastructure()
    {
        var referencedAssemblies = ReferencedAssemblyNames(typeof(Domain.AssemblyMarker).Assembly);

        Assert.DoesNotContain("Salonera.Application", referencedAssemblies);
        Assert.DoesNotContain("Salonera.Infrastructure", referencedAssemblies);
        Assert.DoesNotContain("Salonera.Desktop", referencedAssemblies);
    }

    [Fact]
    public void ApplicationDoesNotReferenceInfrastructureOrDesktop()
    {
        var referencedAssemblies = ReferencedAssemblyNames(typeof(Application.AssemblyMarker).Assembly);

        Assert.DoesNotContain("Salonera.Infrastructure", referencedAssemblies);
        Assert.DoesNotContain("Salonera.Desktop", referencedAssemblies);
    }

    private static string[] ReferencedAssemblyNames(Assembly assembly)
    {
        return assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();
    }
}

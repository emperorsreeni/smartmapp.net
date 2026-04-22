using System.Reflection;
using System.Runtime.CompilerServices;
using FluentAssertions;
using SmartMapp.Net.DependencyInjection.Internal;

namespace SmartMapp.Net.Tests.Unit.DependencyInjection;

/// <summary>
/// Sprint 8 • S8-T00 — package scaffolding tests for
/// <c>SmartMapp.Net.DependencyInjection</c>. These assertions lock the
/// NuGet-facing metadata (assembly name, product, internals-visible-to)
/// so later Sprint 8 tasks do not accidentally break the package surface.
/// </summary>
public class PackageMetadataTests
{
    private static Assembly PackageAssembly => typeof(AssemblyMarker).Assembly;

    [Fact]
    public void AssemblyName_MatchesPackageId()
    {
        PackageAssembly
            .GetName().Name
            .Should().Be("SmartMapp.Net.DependencyInjection");
    }

    [Fact]
    public void AssemblyProduct_IsSmartMappNet()
    {
        var product = PackageAssembly.GetCustomAttribute<AssemblyProductAttribute>();

        product.Should().NotBeNull();
        product!.Product.Should().Be("SmartMapp.Net");
    }

    [Fact]
    public void AssemblyCompany_IsSmartMappNet()
    {
        var company = PackageAssembly.GetCustomAttribute<AssemblyCompanyAttribute>();

        company.Should().NotBeNull();
        company!.Company.Should().Be("SmartMapp.Net");
    }

    [Fact]
    public void AssemblyVersion_IsNotZero()
    {
        PackageAssembly
            .GetName().Version
            .Should().NotBeNull()
            .And.NotBe(new Version(0, 0, 0, 0));
    }

    [Fact]
    public void PublicKey_IsEmpty_AssemblyIsUnsigned()
    {
        // S8-T00 ships an unsigned assembly. Strong-naming / public-key signing
        // is deferred to the v1.0 release work (Sprint 8 S8-T12). This test locks
        // the current state and will fail loudly if signing is introduced so that
        // PackageMetadataTests can be updated deliberately.
        var name = PackageAssembly.GetName();

        (name.GetPublicKey() ?? []).Should().BeEmpty(
            "SmartMapp.Net.DependencyInjection is not strong-named at S8-T00 scope.");
        (name.GetPublicKeyToken() ?? []).Should().BeEmpty(
            "SmartMapp.Net.DependencyInjection has no public-key token at S8-T00 scope.");
    }

    [Fact]
    public void InternalsVisibleTo_ExposesTestAssemblies()
    {
        var ivt = PackageAssembly
            .GetCustomAttributes<InternalsVisibleToAttribute>()
            .Select(a => a.AssemblyName)
            .ToArray();

        ivt.Should().Contain("SmartMapp.Net.Tests.Unit");
        ivt.Should().Contain("SmartMapp.Net.Tests.Integration");
    }

    [Fact]
    public void PackageAssembly_PublicSurface_MatchesS8T06Scope()
    {
        // Public-surface invariant, updated deliberately per sprint task so growth is reviewable:
        //   S8-T00 — 0 public types (scaffolding only)
        //   S8-T01 — +1 SculptorServiceCollectionExtensions (AddSculptor() core)
        //   S8-T02 — no new types (overloads added to existing extension class)
        //   S8-T03 — +1 DependencyInjectionMapper<,> (open-generic IMapper<,> wrapper)
        //   S8-T04 — no new DI-package public types (core added IProviderResolver + DefaultProviderResolver;
        //            DI package's DependencyInjectionProviderFactory / DependencyInjectionSculptor / ServiceProviderAmbientAccessor are all internal)
        //   S8-T05 — +2 SculptorStartupValidator (IHostedService) + SculptorStartupValidationException
        //   S8-T06 — +1 SculptorQueryableExtensions (IQueryable.SelectAs<T>)
        var publicTypes = PackageAssembly
            .GetExportedTypes()
            .Where(t => !t.IsNested)
            .Select(t => t.FullName ?? t.Name)
            // Strip the generic arity marker so the comparison below reads naturally.
            .Select(n => n!.Split('`')[0])
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        publicTypes.Should().BeEquivalentTo(
            [
                "Microsoft.Extensions.DependencyInjection.SculptorServiceCollectionExtensions",
                "SmartMapp.Net.DependencyInjection.DependencyInjectionMapper",
                "SmartMapp.Net.DependencyInjection.Exceptions.SculptorStartupValidationException",
                "SmartMapp.Net.DependencyInjection.Extensions.SculptorQueryableExtensions",
                "SmartMapp.Net.DependencyInjection.SculptorStartupValidator",
            ],
            "S8-T06 adds SculptorQueryableExtensions for IQueryable.SelectAs<TTarget> projection.");
    }

    [Fact]
    public void RuntimeMoniker_Resolves()
    {
        // Sanity: the test host loaded one of the declared target frameworks.
        var tfm = PackageAssembly
            .GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>();

        tfm.Should().NotBeNull();
        tfm!.FrameworkName.Should().ContainAny(
            ".NETStandard,Version=v2.1",
            ".NETCoreApp,Version=v8.0",
            ".NETCoreApp,Version=v10.0");
    }

    [Fact]
    public void PackageAssembly_ReferencesCoreSmartMappNetAssembly()
    {
        PackageAssembly
            .GetReferencedAssemblies()
            .Select(n => n.Name)
            .Should().Contain("SmartMapp.Net");
    }

    [Fact]
    public void PackageAssembly_ReferencesMicrosoftExtensionsDependencyInjectionAbstractions()
    {
        PackageAssembly
            .GetReferencedAssemblies()
            .Select(n => n.Name)
            .Should().Contain("Microsoft.Extensions.DependencyInjection.Abstractions");
    }
}

using Microsoft.CodeAnalysis.Testing;

namespace E128.Analyzers.Tests;

/// <summary>
///     Named reference assembly sets shared by the test files. One place owns the
///     package pins.
/// </summary>
internal static class TestReferences
{
    /// <summary>
    ///     Net 10.0 with the Microsoft.Extensions.DependencyInjection assembly.
    /// </summary>
    public static readonly ReferenceAssemblies Net100WithDependencyInjection = ReferenceAssemblies.Net.Net100
        .AddPackages([new PackageIdentity("Microsoft.Extensions.DependencyInjection", "10.0.14")]);

    /// <summary>
    ///     Net 10.0 with the dependency injection and Entity Framework Core assemblies.
    /// </summary>
    public static readonly ReferenceAssemblies Net100WithEfCore = ReferenceAssemblies.Net.Net100
        .AddPackages([
            new PackageIdentity("Microsoft.Extensions.DependencyInjection", "10.0.14"),
            new PackageIdentity("Microsoft.EntityFrameworkCore", "10.0.14")
        ]);

    /// <summary>
    ///     Net 10.0 with the dependency injection and hosting abstractions assemblies.
    /// </summary>
    public static readonly ReferenceAssemblies Net100WithDependencyInjectionAndHosting = ReferenceAssemblies.Net.Net100
        .AddPackages([
            new PackageIdentity("Microsoft.Extensions.DependencyInjection", "10.0.14"),
            new PackageIdentity("Microsoft.Extensions.Hosting.Abstractions", "10.0.14")
        ]);

    /// <summary>
    ///     Net 10.0 with the dependency injection and HTTP factories assemblies.
    /// </summary>
    public static readonly ReferenceAssemblies Net100WithDependencyInjectionAndHttp = ReferenceAssemblies.Net.Net100
        .AddPackages([
            new PackageIdentity("Microsoft.Extensions.DependencyInjection", "10.0.14"),
            new PackageIdentity("Microsoft.Extensions.Http", "10.0.14")
        ]);

    /// <summary>
    ///     Net 10.0 with the real Microsoft.IdentityModel token packages.
    /// </summary>
    public static readonly ReferenceAssemblies Net100WithJwt = ReferenceAssemblies.Net.Net100
        .AddPackages([
            new PackageIdentity("System.IdentityModel.Tokens.Jwt", "8.23.0"),
            new PackageIdentity("Microsoft.IdentityModel.JsonWebTokens", "8.23.0")
        ]);

    /// <summary>
    ///     Net 10.0 with the pinned analyzer testing package.
    /// </summary>
    public static readonly ReferenceAssemblies Net100WithAnalyzerTesting = ReferenceAssemblies.Net.Net100
        .AddPackages([new PackageIdentity("Microsoft.CodeAnalysis.CSharp.Analyzer.Testing", "1.1.3")]);
}

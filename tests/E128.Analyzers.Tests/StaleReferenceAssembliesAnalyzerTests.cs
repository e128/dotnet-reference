using System.Globalization;
using System.Threading.Tasks;
using E128.Analyzers.Testing;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class StaleReferenceAssembliesAnalyzerTests
{
    private static readonly ReferenceAssemblies Net100WithTesting = ReferenceAssemblies.Net.Net100
        .AddPackages([new PackageIdentity("Microsoft.CodeAnalysis.CSharp.Analyzer.Testing", "1.1.3")]);

    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<StaleReferenceAssembliesAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = Net100WithTesting
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync(TestContext.Current.CancellationToken);
    }

    private static Task VerifyWithMinimumVersionAsync(int minimumVersion, string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<StaleReferenceAssembliesAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = Net100WithTesting
        };
        test.TestState.AnalyzerConfigFiles.Add(
            ("/.globalconfig", $"is_global = true\ne128_minimum_framework_version = {minimumVersion.ToString(CultureInfo.InvariantCulture)}"));
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync(TestContext.Current.CancellationToken);
    }

    #region Does not fire

    [Fact]
    [Trait("Category", "CI")]
    public Task DoesNotFire_WhenNet100()
    {
        return VerifyAsync("""
                           using Microsoft.CodeAnalysis.Testing;
                           class Tests
                           {
                               private static readonly ReferenceAssemblies Assemblies = ReferenceAssemblies.Net.Net100;
                           }
                           """);
    }

    #endregion Does not fire

    #region Fires

    [Fact]
    [Trait("Category", "CI")]
    public Task FiresOnNet80_WhenDefaultMinimumIs100()
    {
        return VerifyAsync("""
                           using Microsoft.CodeAnalysis.Testing;
                           class Tests
                           {
                               private static readonly ReferenceAssemblies Assemblies = {|E128062:ReferenceAssemblies.Net.Net80|};
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FiresOnNet90_WhenDefaultMinimumIs100()
    {
        return VerifyAsync("""
                           using Microsoft.CodeAnalysis.Testing;
                           class Tests
                           {
                               private static readonly ReferenceAssemblies Assemblies = {|E128062:ReferenceAssemblies.Net.Net90|};
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FiresOnNet80_InFieldAssignment()
    {
        return VerifyAsync("""
                           using Microsoft.CodeAnalysis.Testing;
                           class Tests
                           {
                               private static readonly ReferenceAssemblies Assemblies = {|E128062:ReferenceAssemblies.Net.Net80|};
                           }
                           """);
    }

    /// <summary>
    ///     The pinned testing package does not expose <c lang="csharp">ReferenceAssemblies.Net.Net110</c>, so the
    ///     snippet declares the member itself. The alias keeps the leftmost identifier reading
    ///     "ReferenceAssemblies", which is the shape the analyzer matches on.
    /// </summary>
    [Fact]
    [Trait("Category", "CI")]
    public Task FiresOnNet110_WhenMinimumIs120()
    {
        return VerifyWithMinimumVersionAsync(120, """
                                                  using Microsoft.CodeAnalysis.Testing;
                                                  using ReferenceAssemblies = Fake.ReferenceAssemblies;

                                                  class Tests
                                                  {
                                                      private static readonly object Assemblies = {|E128062:ReferenceAssemblies.Net.Net110|};
                                                  }

                                                  namespace Fake
                                                  {
                                                      static class ReferenceAssemblies
                                                      {
                                                          public static class Net
                                                          {
                                                              public static object Net110 { get; }
                                                          }
                                                      }
                                                  }
                                                  """);
    }

    #endregion Fires
}

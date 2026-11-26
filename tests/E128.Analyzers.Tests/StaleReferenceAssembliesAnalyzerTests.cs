using System.Globalization;
using System.Threading.Tasks;
using E128.Analyzers.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class StaleReferenceAssembliesAnalyzerTests
{

    private static AnalyzerTestOptions Options => new() { References = TestReferences.Net100WithAnalyzerTesting };

    private static Task VerifyWithMinimumVersionAsync(int minimumVersion, string code, params DiagnosticResult[] expected)
    {
        return AnalyzerTestRunner.VerifyAsync<StaleReferenceAssembliesAnalyzer>(
            code,
            expected,
            new AnalyzerTestOptions
            {
                References = TestReferences.Net100WithAnalyzerTesting,
                AnalyzerConfig = ("/.globalconfig", $"is_global = true\ne128_minimum_framework_version = {minimumVersion.ToString(CultureInfo.InvariantCulture)}")
            });
    }

    #region Does not fire

    [Fact]
    [Trait("Category", "CI")]
    public Task DoesNotFire_WhenNet100()
    {
        return AnalyzerTestRunner.VerifyAsync<StaleReferenceAssembliesAnalyzer>("""
                           using Microsoft.CodeAnalysis.Testing;
                           class Tests
                           {
                               private static readonly ReferenceAssemblies Assemblies = ReferenceAssemblies.Net.Net100;
                           }
                           """, options: Options);
    }

    #endregion Does not fire

    #region Fires

    [Fact]
    [Trait("Category", "CI")]
    public Task FiresOnNet80_WhenDefaultMinimumIs100()
    {
        return AnalyzerTestRunner.VerifyAsync<StaleReferenceAssembliesAnalyzer>("""
                           using Microsoft.CodeAnalysis.Testing;
                           class Tests
                           {
                               private static readonly ReferenceAssemblies Assemblies = {|E128062:ReferenceAssemblies.Net.Net80|};
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FiresOnNet90_WhenDefaultMinimumIs100()
    {
        return AnalyzerTestRunner.VerifyAsync<StaleReferenceAssembliesAnalyzer>("""
                           using Microsoft.CodeAnalysis.Testing;
                           class Tests
                           {
                               private static readonly ReferenceAssemblies Assemblies = {|E128062:ReferenceAssemblies.Net.Net90|};
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FiresOnNet80_InFieldAssignment()
    {
        return AnalyzerTestRunner.VerifyAsync<StaleReferenceAssembliesAnalyzer>("""
                           using Microsoft.CodeAnalysis.Testing;
                           class Tests
                           {
                               private static readonly ReferenceAssemblies Assemblies = {|E128062:ReferenceAssemblies.Net.Net80|};
                           }
                           """, options: Options);
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

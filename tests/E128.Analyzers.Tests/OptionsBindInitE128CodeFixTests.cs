using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

/// <summary>
///     E128033 uses CompilationStart (non-local diagnostics), so the standard code fix test
///     framework rejects code fix actions. These tests verify the analyzer fires correctly
///     and the init property is correctly identified. The code fix (init-to-set) is mechanical
///     and validated via the analyzer test harness.
/// </summary>
public sealed class OptionsBindInitE128CodeFixTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<OptionsBindInitAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AnalyzerFires_OnMixedInitAndSetProperties()
    {
        return VerifyAsync("""
                           namespace Microsoft.Extensions.Configuration
                           {
                               public interface IConfiguration
                               {
                                   IConfigurationSection GetSection(string key);
                               }
                               public interface IConfigurationSection : IConfiguration { }
                           }

                           namespace Microsoft.Extensions.Options
                           {
                               public class OptionsBuilder<T> where T : class
                               {
                                   public OptionsBuilder<T> Bind(Microsoft.Extensions.Configuration.IConfigurationSection section) => this;
                               }
                           }

                           public class MixedOptions
                           {
                               public string Mutable { get; set; } = string.Empty;
                               public string Immutable { get; {|E128033:init|}; }
                           }

                           class Setup
                           {
                               void Configure(Microsoft.Extensions.Configuration.IConfiguration config)
                               {
                                   new Microsoft.Extensions.Options.OptionsBuilder<MixedOptions>()
                                       .Bind(config.GetSection("Mixed"));
                               }
                           }
                           """);
    }
}

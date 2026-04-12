using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

/// <summary>
/// E128034 has an intentionally empty code fix provider (the fix is too complex to automate).
/// The analyzer uses CompilationEnd (non-local diagnostics), so code fix testing is not possible
/// via the standard framework. These tests verify the analyzer fires at the expected locations.
/// </summary>
public sealed class ConstructorNewDiRegisteredE128CodeFixTests
{
    private static readonly ReferenceAssemblies Net80WithDi = ReferenceAssemblies.Net.Net80
        .AddPackages([new PackageIdentity("Microsoft.Extensions.DependencyInjection", "8.0.0")]);

    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<ConstructorNewDiRegisteredAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = Net80WithDi,
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AnalyzerFires_WhenConstructorNewsRegisteredType()
    {
        return VerifyAsync("""
            using Microsoft.Extensions.DependencyInjection;
            class MyService { }
            class Consumer
            {
                public Consumer()
                {
                    var svc = {|E128034:new MyService()|};
                }
            }
            class Startup
            {
                void Configure(IServiceCollection services)
                {
                    services.AddSingleton<MyService>();
                }
            }
            """);
    }
}

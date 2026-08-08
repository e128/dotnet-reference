using System.Threading.Tasks;
using E128.Analyzers.Testing;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class TestCodeCommentAnalyzerTests
{
    private static readonly ReferenceAssemblies Net100WithXunit = ReferenceAssemblies.Net.Net100
        .AddPackages([new PackageIdentity("xunit.v3.core", "3.2.2")]);

    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<TestCodeCommentAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = Net100WithXunit
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Analyzer_Reports_WhenFactBodyHasLineComment()
    {
        return VerifyAsync("""
                            using Xunit;

                            public class C
                            {
                                [Fact]
                                public void M()
                                {
                                    {|E128097:// comment|}
                                    var x = 1;
                                }
                            }
                            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Analyzer_Reports_WhenTestClassHelperHasDocComment()
    {
        return VerifyAsync("""
                            using Xunit;

                            public class C
                            {
                                {|E128097:/// <summary>Helper</summary>|}
                                private static void Helper()
                                {
                                }

                                [Fact]
                                public void M()
                                {
                                }
                            }
                            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Analyzer_ReportsNothing_WhenTypeHasNoXunitAttribute()
    {
        return VerifyAsync("""
                            public class D
                            {
                                public void M()
                                {
                                    // comment
                                    var x = 1;
                                }
                            }
                            """);
    }
}

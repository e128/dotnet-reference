using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class HardcodedTmpPathE128AnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<HardcodedTmpPathE128Analyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ExactTmp_Fires()
    {
        return VerifyAsync("""
            class C
            {
                void M()
                {
                    var path = {|E128023:"/tmp"|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TmpPrefix_Fires()
    {
        return VerifyAsync("""
            class C
            {
                void M()
                {
                    var path = {|E128023:"/tmp/foo/cache.db"|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task WindowsTemp_Fires()
    {
        return VerifyAsync("""
            class C
            {
                void M()
                {
                    var path = {|E128023:@"C:\Temp"|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task WindowsWindowsTemp_Fires()
    {
        return VerifyAsync("""
            class C
            {
                void M()
                {
                    var path = {|E128023:@"C:\Windows\Temp\cache.db"|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PathGetTempPath_NoFire()
    {
        return VerifyAsync("""
            class C
            {
                void M()
                {
                    var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cache.db");
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TmpFooWithoutSlash_NoFire()
    {
        return VerifyAsync("""
            class C
            {
                void M()
                {
                    var path = "/tmpfoo";
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TemporaryPath_NoFire()
    {
        return VerifyAsync("""
            class C
            {
                void M()
                {
                    var path = "/temporary";
                }
            }
            """);
    }
}

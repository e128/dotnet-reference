using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class CatchFilterOceE128AnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<CatchFilterOceAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CatchFilter_MissingOce_FiresE128039()
    {
        return VerifyAsync("""
            using System;
            class C
            {
                void M()
                {
                    try { }
                    {|E128039:catch (Exception ex) when (ex is not OutOfMemoryException)|}
                    {
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CatchFilter_IncludesOce_DoesNotFire()
    {
        return VerifyAsync("""
            using System;
            class C
            {
                void M()
                {
                    try { }
                    catch (Exception ex) when (ex is not OutOfMemoryException and not OperationCanceledException)
                    {
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CatchFilter_IncludesTaskCanceledException_DoesNotFire()
    {
        return VerifyAsync("""
            using System;
            using System.Threading.Tasks;
            class C
            {
                void M()
                {
                    try { }
                    catch (Exception ex) when (ex is not OutOfMemoryException and not TaskCanceledException)
                    {
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CatchFilter_PrecedingCatchHandlesOce_DoesNotFire()
    {
        return VerifyAsync("""
            using System;
            class C
            {
                void M()
                {
                    try { }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex) when (ex is not OutOfMemoryException)
                    {
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CatchFilter_NoFilterAtAll_DoesNotFire()
    {
        return VerifyAsync("""
            using System;
            class C
            {
                void M()
                {
                    try { }
                    catch (Exception)
                    {
                    }
                }
            }
            """);
    }
}

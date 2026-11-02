using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class IndexOfAnyToSearchValuesE128AnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        return VerifyAsync(code, ReferenceAssemblies.Net.Net100, expected);
    }

    private static Task VerifyAsync(
        string code,
        ReferenceAssemblies referenceAssemblies,
        params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<IndexOfAnyToSearchValuesAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = referenceAssemblies
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task IndexOfAny_CharArrayCreation_Fires()
    {
        return VerifyAsync("""
                           class C
                           {
                               void M(string path)
                               {
                                   var i = {|E128102:path.IndexOfAny(new[] { '/', '\\' })|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task IndexOfAny_CharArrayVariable_Fires()
    {
        return VerifyAsync("""
                           class C
                           {
                               void M(string value, char[] separators)
                               {
                                   var i = {|E128102:value.IndexOfAny(separators)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LastIndexOfAny_Fires()
    {
        return VerifyAsync("""
                           class C
                           {
                               void M(string value)
                               {
                                   var i = {|E128102:value.LastIndexOfAny(new[] { '.', '-' })|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task IndexOfAny_NoDiagnostic_WhenSearchValuesIsUnavailable()
    {
        // SearchValues<T> arrived in .NET 8. On netstandard2.0 the replacement does not compile.
        return VerifyAsync(
            """
            class C
            {
                void M(string path)
                {
                    var i = path.IndexOfAny(new[] { '/', '\\' });
                }
            }
            """,
            ReferenceAssemblies.NetStandard.NetStandard20);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SpanIndexOfAny_DoesNotFire()
    {
        return VerifyAsync("""
                           using System;
                           class C
                           {
                               void M(ReadOnlySpan<char> span)
                               {
                                   var i = span.IndexOfAny(stackalloc char[] { '/', '\\' });
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CustomTypeIndexOfAny_DoesNotFire()
    {
        return VerifyAsync("""
                           class C
                           {
                               void M()
                               {
                                   var list = new System.Collections.Generic.List<int>();
                                   var i = list.IndexOfAny();
                               }
                           }

                           static class Extensions
                           {
                               public static int IndexOfAny(this System.Collections.Generic.List<int> list)
                               {
                                   return -1;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task StringIndexOf_DoesNotFire()
    {
        return VerifyAsync("""
                           class C
                           {
                               void M(string value)
                               {
                                   var i = value.IndexOf('/');
                               }
                           }
                           """);
    }
}

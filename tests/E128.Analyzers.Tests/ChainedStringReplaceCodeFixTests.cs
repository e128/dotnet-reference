using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ChainedStringReplaceCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<ChainedStringReplaceAnalyzer, ChainedStringReplaceCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        }.RunAsync();
    }

    private static Task VerifyNoFixAsync(string source, params DiagnosticResult[] expected)
    {
        var test = new CSharpCodeFixTest<ChainedStringReplaceAnalyzer, ChainedStringReplaceCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            NumberOfFixAllInDocumentIterations = 0
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DoubledHyphenCollapseLoop_FixedToSinglePass()
    {
        return VerifyFixAsync(
            """
            using System;
            class C
            {
                void M(string value)
                {
                    {|E128098:while (value.Contains("--", StringComparison.Ordinal))
                    {
                        value = value.Replace("--", "-", StringComparison.Ordinal);
                    }|}
                }
            }
            """,
            """
            using System;
            class C
            {
                void M(string value)
                {
                    {
                        var __buffer = new char[value.Length];
                        var __pos = 0;
                        var __collapsing = false;
                        foreach (var __c in value)
                        {
                            if (__c == '-')
                            {
                                if (!__collapsing)
                                {
                                    __buffer[__pos++] = __c;
                                    __collapsing = true;
                                }
                            }
                            else
                            {
                                __buffer[__pos++] = __c;
                                __collapsing = false;
                            }
                        }

                        value = new string(__buffer, 0, __pos);
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NonDoubledLiteralLoop_NoFixOffered()
    {
        return VerifyNoFixAsync("""
                                 using System;
                                 class C
                                 {
                                     void M(string value)
                                     {
                                         {|E128098:while (value.Contains("ab", StringComparison.Ordinal))
                                         {
                                             value = value.Replace("ab", "a", StringComparison.Ordinal);
                                         }|}
                                     }
                                 }
                                 """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ChainShape_NoFixOffered()
    {
        return VerifyNoFixAsync("""
                                 class C
                                 {
                                     void M(string value)
                                     {
                                         var result = {|E128098:value.Replace('\n', ' ').Replace('\r', ' ')|};
                                     }
                                 }
                                 """);
    }
}

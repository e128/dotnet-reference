using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class StringFormatToInterpolationE128CodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<StringFormatToInterpolationAnalyzer, StringFormatToInterpolationCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            NumberOfFixAllIterations = 1
        }.RunAsync();
    }

    private static Task VerifyNoFixAsync(string source)
    {
        return new CSharpCodeFixTest<StringFormatToInterpolationAnalyzer, StringFormatToInterpolationCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            NumberOfFixAllInDocumentIterations = 0
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task StringFormat_SingleArg_RewrittenToInterpolation()
    {
        return VerifyFixAsync(
            """
            using System;
            class C
            {
                void M()
                {
                    var name = "world";
                    var s = {|E128015:string.Format("Hello {0}", name)|};
                }
            }
            """,
            """
            using System;
            class C
            {
                void M()
                {
                    var name = "world";
                    var s = $"Hello {name}";
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task StringFormat_MultipleArgs_RewrittenToInterpolation()
    {
        return VerifyFixAsync(
            """
            using System;
            class C
            {
                void M()
                {
                    var a = "foo";
                    var b = 42;
                    var s = {|E128015:string.Format("{0} is {1}", a, b)|};
                }
            }
            """,
            """
            using System;
            class C
            {
                void M()
                {
                    var a = "foo";
                    var b = 42;
                    var s = $"{a} is {b}";
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task StringFormat_EscapesInTheLiteralText_StayEscaped()
    {
        // The format string holds the runtime value. A raw line break breaks the emitted literal.
        return VerifyFixAsync(
            """
            using System;
            class C
            {
                void M()
                {
                    var name = "world";
                    var s = {|E128015:string.Format("Hello {0}\n", name)|};
                }
            }
            """,
            """
            using System;
            class C
            {
                void M()
                {
                    var name = "world";
                    var s = $"Hello {name}\n";
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task StringFormat_UnmatchedBrace_NoFixOffered()
    {
        // An unmatched brace cannot be reproduced in an interpolated string, so the fix declines
        // instead of looping on the same position.
        return VerifyNoFixAsync(
            """
            using System;
            class C
            {
                void M()
                {
                    var name = "world";
                    var s = {|E128015:string.Format("{0}}", name)|};
                }
            }
            """);
    }
}

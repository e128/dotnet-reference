using System.Threading.Tasks;
using E128.Analyzers.Style;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ObsoleteWithoutMessageAnalyzerTests
{
    private static Task VerifyAsync(string code)
    {
        return new CSharpAnalyzerTest<ObsoleteWithoutMessageAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task BareObsolete_Fires()
    {
        return VerifyAsync("""
                           using System;
                           class C
                           {
                               [{|E128169:Obsolete|}]
                               void Old() { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task EmptyMessage_Fires()
    {
        return VerifyAsync("""
                           using System;
                           class C
                           {
                               [{|E128169:Obsolete("")|}]
                               void Old() { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task OnlyNamedArguments_Fires()
    {
        return VerifyAsync("""
                           using System;
                           class C
                           {
                               [{|E128169:Obsolete(DiagnosticId = "X1")|}]
                               void Old() { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task WithMessage_Silent()
    {
        return VerifyAsync("""
                           using System;
                           class C
                           {
                               [Obsolete("Use New instead.")]
                               void Old() { }

                               [ObsoleteAttribute("Use New instead.", true)]
                               void Older() { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task OtherAttribute_Silent()
    {
        return VerifyAsync("""
                           using System;
                           sealed class ObsoleteAttribute : Attribute { }
                           class C
                           {
                               [Obsolete]
                               void Old() { }
                           }
                           """);
    }
}

using System.Threading.Tasks;
using E128.Analyzers.Style;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ObsoleteWithoutMessageAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task BareObsolete_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<ObsoleteWithoutMessageAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<ObsoleteWithoutMessageAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<ObsoleteWithoutMessageAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<ObsoleteWithoutMessageAnalyzer>("""
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
    public Task NullMessage_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<ObsoleteWithoutMessageAnalyzer>("""
                           using System;
                           class C
                           {
                               [{|E128169:Obsolete(null)|}]
                               void Old() { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ReorderedNamedArgumentsWithEmptyMessage_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<ObsoleteWithoutMessageAnalyzer>("""
                           using System;
                           class C
                           {
                               [{|E128169:Obsolete(error: true, message: "")|}]
                               void Old() { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ReorderedNamedArgumentsWithMessage_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<ObsoleteWithoutMessageAnalyzer>("""
                           using System;
                           class C
                           {
                               [Obsolete(error: true, message: "Use New instead.")]
                               void Old() { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task OtherAttribute_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<ObsoleteWithoutMessageAnalyzer>("""
                           using System;
                           sealed class ObsoleteAttribute : Attribute { }
                           class C
                           {
                               [Obsolete]
                               void Old() { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PartialPropertyWithoutImplementation_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<ObsoleteWithoutMessageAnalyzer>("""
                           using System;
                           partial class C
                           {
                               [{|E128169:Obsolete|}]
                               private static partial int {|CS9248:P|} { get; }
                           }
                           """);
    }
}

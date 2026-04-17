using System.Threading.Tasks;
using E128.Analyzers.Style;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class NullForgivingOperatorE128AnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<NullForgivingOperatorAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NullForgiving_OnVariableAssignment_Fires()
    {
        return VerifyAsync("""
                           #nullable enable
                           class C
                           {
                               void M(string? s)
                               {
                                   string x = {|E128043:s!|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NullForgiving_OnMethodReturn_Fires()
    {
        return VerifyAsync("""
                           #nullable enable
                           class C
                           {
                               string M(string? s) => {|E128043:s!|};
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NullForgiving_OnPropertyAccess_Fires()
    {
        return VerifyAsync("""
                           #nullable enable
                           class C
                           {
                               void M(string? s)
                               {
                                   var len = {|E128043:s!|}.Length;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoNullForgiving_NoDiagnostic()
    {
        return VerifyAsync("""
                           #nullable enable
                           class C
                           {
                               void M(string? s)
                               {
                                   if (s is not null)
                                   {
                                       string x = s;
                                   }
                               }
                           }
                           """);
    }
}

using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class StringBuilderPoolAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<StringBuilderPoolAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenBareStringBuilderAllocated()
    {
        return VerifyAsync("""
                           using System.Text;
                           class C
                           {
                               void M()
                               {
                                   var sb = {|E128081:new StringBuilder()|};
                                   sb.Append("hello");
                                   _ = sb.ToString();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenStringBuilderAllocatedWithCapacity()
    {
        return VerifyAsync("""
                           using System.Text;
                           class C
                           {
                               void M()
                               {
                                   var sb = {|E128081:new StringBuilder(1024)|};
                                   sb.Append("hello");
                                   _ = sb.ToString();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenImplicitObjectCreation()
    {
        return VerifyAsync("""
                           using System.Text;
                           class C
                           {
                               void M()
                               {
                                   StringBuilder sb = {|E128081:new(1024)|};
                                   sb.Append("hello");
                                   _ = sb.ToString();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoDiagnostic_WhenInsidePoolImplementation()
    {
        return VerifyAsync("""
                           using System.Text;
                           class DefaultStringBuilderPool
                           {
                               StringBuilder Create()
                               {
                                   return new StringBuilder(100);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoDiagnostic_WhenNoStringBuilder()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           class C
                           {
                               void M()
                               {
                                   var list = new List<int>();
                               }
                           }
                           """);
    }
}

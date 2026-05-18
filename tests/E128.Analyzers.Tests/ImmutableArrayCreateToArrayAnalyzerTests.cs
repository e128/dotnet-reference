using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ImmutableArrayCreateToArrayAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<ImmutableArrayCreateToArrayAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenCreateWrapsToArray()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Collections.Immutable;
                           class C
                           {
                               void M()
                               {
                                   var list = new List<int> { 1, 2, 3 };
                                   var arr = {|E128083:ImmutableArray.Create(list.ToArray())|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenCreateRangeWrapsToArray()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Collections.Immutable;
                           class C
                           {
                               void M()
                               {
                                   var list = new List<int> { 1, 2, 3 };
                                   var arr = {|E128083:ImmutableArray.CreateRange(list.ToArray())|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenCreateWrapsLocalArray()
    {
        return VerifyAsync("""
                           using System.Collections.Immutable;
                           class C
                           {
                               void M()
                               {
                                   var data = new int[] { 1, 2, 3 };
                                   var arr = ImmutableArray.Create(data);
                               }
                           }
                           """);
    }
}

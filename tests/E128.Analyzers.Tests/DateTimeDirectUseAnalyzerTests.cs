using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class DateTimeDirectUseAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<DateTimeDirectUseAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DateTime_Now_InMethod_Fires()
    {
        return VerifyAsync("""
                           using System;
                           class C
                           {
                               void M()
                               {
                                   var x = {|E128003:DateTime.Now|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DateTime_UtcNow_InMethod_Fires()
    {
        return VerifyAsync("""
                           using System;
                           class C
                           {
                               void M()
                               {
                                   var x = {|E128003:DateTime.UtcNow|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DateTime_Today_InMethod_Fires()
    {
        return VerifyAsync("""
                           using System;
                           class C
                           {
                               void M()
                               {
                                   var x = {|E128003:DateTime.Today|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DateTimeOffset_Now_InMethod_Fires()
    {
        return VerifyAsync("""
                           using System;
                           class C
                           {
                               void M()
                               {
                                   var x = {|E128003:DateTimeOffset.Now|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DateTime_UtcNow_InStaticFieldInitializer_NoFire()
    {
        return VerifyAsync("""
                           using System;
                           class C
                           {
                               private static readonly DateTime _baseline = DateTime.UtcNow;
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DateTimeOffset_UtcNow_InStaticReadonlyField_NoFire()
    {
        return VerifyAsync("""
                           using System;
                           class C
                           {
                               private static readonly DateTimeOffset _ts = DateTimeOffset.UtcNow;
                           }
                           """);
    }
}

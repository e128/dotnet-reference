using System.Threading.Tasks;
using E128.Analyzers.Design;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class DateTimeDirectUseAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task DateTime_Now_InMethod_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<DateTimeDirectUseAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<DateTimeDirectUseAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<DateTimeDirectUseAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<DateTimeDirectUseAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<DateTimeDirectUseAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<DateTimeDirectUseAnalyzer>("""
                           using System;
                           class C
                           {
                               private static readonly DateTimeOffset _ts = DateTimeOffset.UtcNow;
                           }
                           """);
    }
}

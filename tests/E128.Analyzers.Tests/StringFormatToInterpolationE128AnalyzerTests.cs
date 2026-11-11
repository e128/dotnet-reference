using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class StringFormatToInterpolationE128AnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task StringFormat_WithLiteralAndArgs_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<StringFormatToInterpolationAnalyzer>("""
                           using System;
                           class C
                           {
                               void M()
                               {
                                   var name = "world";
                                   var s = {|E128015:string.Format("Hello {0}", name)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task StringFormat_WithFormatProvider_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<StringFormatToInterpolationAnalyzer>("""
                           using System;
                           using System.Globalization;
                           class C
                           {
                               void M()
                               {
                                   var n = 42;
                                   var s = {|E128015:string.Format(CultureInfo.InvariantCulture, "Value: {0}", n)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task StringFormat_VariableFormatString_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<StringFormatToInterpolationAnalyzer>("""
                           using System;
                           class C
                           {
                               void M(string fmt)
                               {
                                   var s = string.Format(fmt, 42);
                               }
                           }
                           """);
    }
}

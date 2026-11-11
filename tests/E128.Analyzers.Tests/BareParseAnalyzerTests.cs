using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class BareParseAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task IntParse_InFinallyBlock_FiresE128089()
    {
        // The catch clauses guard only the try block, so the finally block stays unguarded.
        return AnalyzerTestRunner.VerifyAsync<BareParseAnalyzer>("""
                           using System;
                           class C
                           {
                               void M(string input)
                               {
                                   try
                                   {
                                   }
                                   catch (Exception)
                                   {
                                   }
                                   finally
                                   {
                                       var n = {|E128089:int.Parse(input)|};
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task IntParse_FiresE128089()
    {
        return AnalyzerTestRunner.VerifyAsync<BareParseAnalyzer>("""
                           using System;
                           class C
                           {
                               void M(string input)
                               {
                                   int id = {|E128089:int.Parse(input)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task GuidParse_FiresE128089()
    {
        return AnalyzerTestRunner.VerifyAsync<BareParseAnalyzer>("""
                           using System;
                           class C
                           {
                               void M(string input)
                               {
                                   var id = {|E128089:Guid.Parse(input)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TimeSpanParse_FiresE128089()
    {
        return AnalyzerTestRunner.VerifyAsync<BareParseAnalyzer>("""
                           using System;
                           class C
                           {
                               void M(string input)
                               {
                                   var ts = {|E128089:TimeSpan.Parse(input)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DecimalParse_FiresE128089()
    {
        return AnalyzerTestRunner.VerifyAsync<BareParseAnalyzer>("""
                           using System;
                           class C
                           {
                               void M(string input)
                               {
                                   var val = {|E128089:decimal.Parse(input)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DoubleParse_FiresE128089()
    {
        return AnalyzerTestRunner.VerifyAsync<BareParseAnalyzer>("""
                           using System;
                           class C
                           {
                               void M(string input)
                               {
                                   var val = {|E128089:double.Parse(input)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task BoolParse_FiresE128089()
    {
        return AnalyzerTestRunner.VerifyAsync<BareParseAnalyzer>("""
                           using System;
                           class C
                           {
                               void M(string input)
                               {
                                   var val = {|E128089:bool.Parse(input)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DateTimeParse_FiresE128089()
    {
        return AnalyzerTestRunner.VerifyAsync<BareParseAnalyzer>("""
                           using System;
                           class C
                           {
                               void M(string input)
                               {
                                   var val = {|E128089:DateTime.Parse(input)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task EnumParse_FiresE128089()
    {
        return AnalyzerTestRunner.VerifyAsync<BareParseAnalyzer>("""
                           using System;
                           class C
                           {
                               enum MyEnum { A, B }
                               void M(string input)
                               {
                                   var val = {|E128089:Enum.Parse<MyEnum>(input)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task IntParse_InTryCatchFormatException_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<BareParseAnalyzer>("""
                           using System;
                           class C
                           {
                               void M(string input)
                               {
                                   try
                                   {
                                       int id = int.Parse(input);
                                   }
                                   catch (FormatException)
                                   {
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task IntParse_InTryCatchException_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<BareParseAnalyzer>("""
                           using System;
                           class C
                           {
                               void M(string input)
                               {
                                   try
                                   {
                                       int id = int.Parse(input);
                                   }
                                   catch (Exception)
                                   {
                                   }
                               }
                           }
                           """);
    }
}

using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class BareParseAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<BareParseAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task IntParse_FiresE128089()
    {
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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

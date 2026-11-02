using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class Utf8StringParseAnalyzerTests
{
    private static Task VerifyAsync(string code)
    {
        return new CSharpAnalyzerTest<Utf8StringParseAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenIntParseReadsAGetStringResult()
    {
        return VerifyAsync("""
                           using System.Text;
                           class C
                           {
                               int M(byte[] bytes)
                               {
                                   return int.Parse({|E128131:Encoding.UTF8.GetString(bytes)|});
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTryParseReadsSpanBytes()
    {
        return VerifyAsync("""
                           using System;
                           using System.Text;
                           class C
                           {
                               bool M(ReadOnlySpan<byte> bytes)
                               {
                                   return int.TryParse({|E128131:Encoding.UTF8.GetString(bytes)|}, out _);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheEncodingIsNotUtf8()
    {
        return VerifyAsync("""
                           using System.Text;
                           class C
                           {
                               int M(byte[] bytes)
                               {
                                   return int.Parse(Encoding.Default.GetString(bytes));
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCallIsNotAParseMethod()
    {
        return VerifyAsync("""
                           using System;
                           using System.Text;
                           class C
                           {
                               void M(byte[] bytes)
                               {
                                   Console.WriteLine(Encoding.UTF8.GetString(bytes));
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheTargetTypeHasNoUtf8Overload()
    {
        return VerifyAsync("""
                           using System.Text;
                           class C
                           {
                               int M(byte[] bytes)
                               {
                                   return My.Parse(Encoding.UTF8.GetString(bytes));
                               }
                           }
                           class My
                           {
                               public static int Parse(string text)
                               {
                                   return text.Length;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheParseTakesAProvider()
    {
        return VerifyAsync("""
                           using System.Globalization;
                           using System.Text;
                           class C
                           {
                               int M(byte[] bytes)
                               {
                                   return int.Parse({|E128131:Encoding.UTF8.GetString(bytes)|},
                                       CultureInfo.InvariantCulture);
                               }
                           }
                           """);
    }
}

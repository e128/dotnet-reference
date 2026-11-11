using System.Threading.Tasks;
using E128.Analyzers.Style;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class EncodingDefaultE128AnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task EncodingDefault_InMethod_FiresE128006()
    {
        return AnalyzerTestRunner.VerifyAsync<EncodingDefaultAnalyzer>("""
                           using System.Text;
                           class C
                           {
                               void M()
                               {
                                   var bytes = {|E128006:Encoding.Default|}.GetBytes("test");
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task EncodingUtf8_InMethod_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<EncodingDefaultAnalyzer>("""
                           using System.Text;
                           class C
                           {
                               void M()
                               {
                                   var bytes = Encoding.UTF8.GetBytes("test");
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task EncodingAscii_InMethod_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<EncodingDefaultAnalyzer>("""
                           using System.Text;
                           class C
                           {
                               void M()
                               {
                                   var bytes = Encoding.ASCII.GetBytes("test");
                               }
                           }
                           """);
    }
}

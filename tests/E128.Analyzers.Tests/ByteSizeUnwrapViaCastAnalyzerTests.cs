using System.Threading.Tasks;
using E128.Analyzers.Design;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ByteSizeUnwrapViaCastAnalyzerTests
{
    private const string ByteSizeStub = """
                                        using Pug.Core.Classes;
                                        namespace Pug.Core.Classes
                                        {
                                            public struct ByteSize
                                            {
                                                public double Bytes { get; set; }
                                                public long Bits { get; set; }
                                                public double Kilobytes { get; set; }
                                                public double Megabytes { get; set; }
                                                public double Gigabytes { get; set; }
                                                public double Terabytes { get; set; }
                                            }
                                        }
                                        """;

    [Fact]
    [Trait("Category", "CI")]
    public Task Fires_OnLongCast_OfBytesProperty()
    {
        return AnalyzerTestRunner.VerifyAsync<ByteSizeUnwrapViaCastAnalyzer>(ByteSizeStub + """
                                          class C
                                          {
                                              void M(ByteSize size)
                                              {
                                                  var x = {|E128082:(long)size.Bytes|};
                                              }
                                          }
                                          """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fires_OnIntCast_OfBytesProperty()
    {
        return AnalyzerTestRunner.VerifyAsync<ByteSizeUnwrapViaCastAnalyzer>(ByteSizeStub + """
                                          class C
                                          {
                                              void M(ByteSize size)
                                              {
                                                  var x = {|E128082:(int)size.Bytes|};
                                              }
                                          }
                                          """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fires_OnDoubleCast_OfKilobytesProperty()
    {
        return AnalyzerTestRunner.VerifyAsync<ByteSizeUnwrapViaCastAnalyzer>(ByteSizeStub + """
                                          class C
                                          {
                                              void M(ByteSize size)
                                              {
                                                  double x = {|E128082:(double)size.Kilobytes|};
                                              }
                                          }
                                          """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoDiagnostic_ForPlainBytesAccess()
    {
        return AnalyzerTestRunner.VerifyAsync<ByteSizeUnwrapViaCastAnalyzer>(ByteSizeStub + """
                                          class C
                                          {
                                              void M(ByteSize size)
                                              {
                                                  var x = size.Bytes;
                                              }
                                          }
                                          """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoDiagnostic_ForCastOnNonByteSizeType()
    {
        return AnalyzerTestRunner.VerifyAsync<ByteSizeUnwrapViaCastAnalyzer>("""
                           class Other
                           {
                               public double Bytes { get; set; }
                           }
                           class C
                           {
                               void M(Other other)
                               {
                                   var x = (long)other.Bytes;
                               }
                           }
                           """);
    }
}

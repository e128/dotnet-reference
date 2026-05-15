using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
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

    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<ByteSizeUnwrapViaCastAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fires_OnLongCast_OfBytesProperty()
    {
        return VerifyAsync(ByteSizeStub + """
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
        return VerifyAsync(ByteSizeStub + """
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
        return VerifyAsync(ByteSizeStub + """
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
        return VerifyAsync(ByteSizeStub + """
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
        return VerifyAsync("""
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

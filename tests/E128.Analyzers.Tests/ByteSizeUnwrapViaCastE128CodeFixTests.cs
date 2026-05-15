using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ByteSizeUnwrapViaCastE128CodeFixTests
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

    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<ByteSizeUnwrapViaCastAnalyzer, ByteSizeUnwrapViaCastCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        }.RunAsync();
    }

    private static Task VerifyNoFixAsync(string source, params DiagnosticResult[] expected)
    {
        var test = new CSharpCodeFixTest<ByteSizeUnwrapViaCastAnalyzer, ByteSizeUnwrapViaCastCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            NumberOfFixAllInDocumentIterations = 0
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DoubleCast_OfDoubleProp_FixRemovesCast()
    {
        return VerifyFixAsync(
            ByteSizeStub + """
                           class C
                           {
                               void M(ByteSize size)
                               {
                                   double x = {|E128082:(double)size.Bytes|};
                               }
                           }
                           """,
            ByteSizeStub + """
                           class C
                           {
                               void M(ByteSize size)
                               {
                                   double x = size.Bytes;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DoubleCast_OfMegabytes_FixRemovesCast()
    {
        return VerifyFixAsync(
            ByteSizeStub + """
                           class C
                           {
                               void M(ByteSize size)
                               {
                                   var x = {|E128082:(double)size.Megabytes|};
                               }
                           }
                           """,
            ByteSizeStub + """
                           class C
                           {
                               void M(ByteSize size)
                               {
                                   var x = size.Megabytes;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LongCast_OfBits_SameType_FixRemovesCast()
    {
        return VerifyFixAsync(
            ByteSizeStub + """
                           class C
                           {
                               void M(ByteSize size)
                               {
                                   long x = {|E128082:(long)size.Bits|};
                               }
                           }
                           """,
            ByteSizeStub + """
                           class C
                           {
                               void M(ByteSize size)
                               {
                                   long x = size.Bits;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task IntCast_OfDoubleProp_NarrowingCast_NoFix()
    {
        return VerifyNoFixAsync(
            ByteSizeStub + """
                           class C
                           {
                               void M(ByteSize size)
                               {
                                   int x = {|E128082:(int)size.Bytes|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LongCast_OfDoubleProp_NarrowingCast_NoFix()
    {
        return VerifyNoFixAsync(
            ByteSizeStub + """
                           class C
                           {
                               void M(ByteSize size)
                               {
                                   long x = {|E128082:(long)size.Bytes|};
                               }
                           }
                           """);
    }
}

using System.Threading.Tasks;
using E128.Analyzers.Design;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ConstructorDependencyCeilingAnalyzerTests
{
    private const string Types = """
                                 interface I1 { } interface I2 { } interface I3 { } interface I4 { }
                                 interface I5 { } interface I6 { } interface I7 { } interface I8 { }
                                 """;

    private static Task VerifyAsync(string code, string? ceiling = null)
    {
        return AnalyzerTestRunner.VerifyAsync<ConstructorDependencyCeilingAnalyzer>(Types + "\n" + code, options: new AnalyzerTestOptions { AnalyzerConfig = ("/.editorconfig", "is_global = true\ne128_max_ctor_dependencies = " + ceiling) });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task EightDependencies_Fires()
    {
        return VerifyAsync("""
                           class C
                           {
                               public {|E128174:C|}(I1 a, I2 b, I3 c, I4 d, I5 e, I6 f, I7 g, I8 h) { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PrimaryConstructor_Fires()
    {
        return VerifyAsync("""
                           class {|E128174:C|}(I1 a, I2 b, I3 c, I4 d, I5 e, I6 f, I7 g, I8 h)
                           {
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SevenDependencies_Silent()
    {
        return VerifyAsync("""
                           class C
                           {
                               public C(I1 a, I2 b, I3 c, I4 d, I5 e, I6 f, I7 g) { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ValuesStringsAndArraysDoNotCount_Silent()
    {
        return VerifyAsync("""
                           class C
                           {
                               public C(I1 a, int b, string c, string d, int[] e, long f, bool g, double h, System.TimeSpan i) { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DataTypesDoNotCount_Silent()
    {
        return VerifyAsync("""
                           class C
                           {
                               public C(
                                   I1 a,
                                   System.Uri b,
                                   object c,
                                   System.Exception d,
                                   System.InvalidOperationException e,
                                   System.Collections.Generic.IReadOnlyList<I2> f,
                                   System.Collections.Generic.IEnumerable<I3> g,
                                   System.Collections.Generic.List<I4> h,
                                   System.Threading.CancellationToken i) { }
                           }
                           """, "1");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task RecordParameterDoesNotCount_Silent()
    {
        return VerifyAsync("""
                           record Dto(int X);
                           class C
                           {
                               public C(I1 a, Dto b) { }
                           }
                           """, "1");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task RecordPrimaryConstructor_Silent()
    {
        return VerifyAsync("""
                           record Holder(I1 A, I2 B, I3 C, I4 D, I5 E, I6 F, I7 G, I8 H);
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ParamsArrayDoesNotCount_Silent()
    {
        return VerifyAsync("""
                           class C
                           {
                               public C(I1 a, params I2[] rest) { }
                           }
                           """, "1");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ServiceClassesCount_Fires()
    {
        return VerifyAsync("""
                           class S1 { } class S2 { }
                           class C
                           {
                               public {|E128174:C|}(I1 a, S1 b, S2 c) { }
                           }
                           """, "2");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CustomCeiling_Applies()
    {
        return VerifyAsync("""
                           class C
                           {
                               public {|E128174:C|}(I1 a, I2 b, I3 c) { }
                           }
                           """, "2");
    }
}

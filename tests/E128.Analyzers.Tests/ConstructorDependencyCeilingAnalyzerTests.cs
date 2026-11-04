using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
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
        var test = new CSharpAnalyzerTest<ConstructorDependencyCeilingAnalyzer, DefaultVerifier>
        {
            TestCode = Types + "\n" + code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };

        if (ceiling is not null)
        {
            test.TestState.AnalyzerConfigFiles.Add(
                ("/.editorconfig", "is_global = true\ne128_max_ctor_dependencies = " + ceiling));
        }

        return test.RunAsync();
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

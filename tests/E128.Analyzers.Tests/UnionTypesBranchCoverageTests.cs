using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UnionTypesBranchCoverageTests
{
    private static INamedTypeSymbol Compile(string code, string typeName)
    {
        var tree = CSharpSyntaxTree.ParseText(code, cancellationToken: TestContext.Current.CancellationToken);
        var compilation = CSharpCompilation.Create(
            "UnionProbe",
            [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        return compilation.GetTypeByMetadataName(typeName)!;
    }

    [Fact]
    [Trait("Category", "CI")]
    public void IsUnion_IsFalseForNull()
    {
        Assert.False(UnionTypes.IsUnion(null));
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("enum Probe { A }")]
    [InlineData("interface Probe { }")]
    [InlineData("delegate void Probe();")]
    public void IsUnion_IsFalseForTypesThatAreNeitherClassNorStruct(string declaration)
    {
        Assert.False(UnionTypes.IsUnion(Compile(declaration, "Probe")));
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("class")]
    [InlineData("struct")]
    public void IsUnion_IsTrueForATypeWithTheCompilerUnionInterface(string kind)
    {
        var code = "namespace System.Runtime.CompilerServices { public interface IUnion { } }\n"
                   + kind + " Probe : System.Runtime.CompilerServices.IUnion { }";
        Assert.True(UnionTypes.IsUnion(Compile(code, "Probe")));
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("namespace Other { public interface IUnion { } }", "Other.IUnion")]
    [InlineData("namespace CompilerServices { public interface IUnion { } }", "CompilerServices.IUnion")]
    [InlineData("namespace Other.CompilerServices { public interface IUnion { } }", "Other.CompilerServices.IUnion")]
    [InlineData("namespace Runtime.CompilerServices { public interface IUnion { } }", "Runtime.CompilerServices.IUnion")]
    [InlineData("namespace Other.Runtime.CompilerServices { public interface IUnion { } }", "Other.Runtime.CompilerServices.IUnion")]
    [InlineData("namespace System.CompilerServices { public interface IUnion { } }", "System.CompilerServices.IUnion")]
    [InlineData("namespace System.Runtime.Other { public interface IUnion { } }", "System.Runtime.Other.IUnion")]
    [InlineData("namespace Outer.System.Runtime.CompilerServices { public interface IUnion { } }", "Outer.System.Runtime.CompilerServices.IUnion")]
    [InlineData("namespace System.Runtime.CompilerServices { public interface IUnionLike { } }", "System.Runtime.CompilerServices.IUnionLike")]
    [InlineData("public interface IUnion { }", "IUnion")]
    public void IsUnion_IsFalseWhenTheInterfaceSitsInAnotherNamespace(string interfaceDeclaration, string interfaceName)
    {
        var code = interfaceDeclaration + "\nclass Probe : " + interfaceName + " { }";
        Assert.False(UnionTypes.IsUnion(Compile(code, "Probe")));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void IsUnion_IsFalseForAPlainClass()
    {
        Assert.False(UnionTypes.IsUnion(Compile("class Probe { }", "Probe")));
    }
}

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class TypeSymbolsBranchCoverageTests
{
    private const string Source = """
        using System;
        using System.Collections.Generic;

        class Base { }
        class Derived : Base { }
        class Leaf : Derived { }
        class Unrelated { }
        class Holder<T> { }
        class Members
        {
            public int? Maybe;
            public int Plain;
            public Holder<int> Closed;
            public List<int> Numbers;
        }
        """;

    private static (CSharpCompilation Compilation, INamedTypeSymbol Members) Compile()
    {
        var tree = CSharpSyntaxTree.ParseText(Source, cancellationToken: TestContext.Current.CancellationToken);
        var compilation = CSharpCompilation.Create(
            "TypeProbe",
            [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        return (compilation, compilation.GetTypeByMetadataName("Members")!);
    }

    private static ITypeSymbol FieldType(INamedTypeSymbol type, string name)
    {
        return ((IFieldSymbol)type.GetMembers(name)[0]).Type;
    }

    [Fact]
    [Trait("Category", "CI")]
    public void UnwrapNullable_ReturnsTheInnerTypeOnlyForNullableOfT()
    {
        var (_, members) = Compile();

        Assert.Equal(SpecialType.System_Int32, TypeSymbols.UnwrapNullable(FieldType(members, "Maybe")).SpecialType);
        Assert.Equal(SpecialType.System_Int32, TypeSymbols.UnwrapNullable(FieldType(members, "Plain")).SpecialType);
        Assert.Same(FieldType(members, "Closed"), TypeSymbols.UnwrapNullable(FieldType(members, "Closed")));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void DerivesFrom_WalksTheBaseChain()
    {
        var (compilation, _) = Compile();
        var baseType = compilation.GetTypeByMetadataName("Base");
        var leaf = compilation.GetTypeByMetadataName("Leaf");
        var unrelated = compilation.GetTypeByMetadataName("Unrelated");

        Assert.True(TypeSymbols.DerivesFrom(leaf, baseType));
        Assert.True(TypeSymbols.DerivesFrom(baseType, baseType));
        Assert.False(TypeSymbols.DerivesFrom(baseType, leaf));
        Assert.False(TypeSymbols.DerivesFrom(unrelated, baseType));
        Assert.False(TypeSymbols.DerivesFrom(leaf, null));
        Assert.False(TypeSymbols.DerivesFrom(null, baseType));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void Matches_ComparesTheTypeWithTheTarget()
    {
        var (compilation, _) = Compile();
        var baseType = compilation.GetTypeByMetadataName("Base");
        var derived = compilation.GetTypeByMetadataName("Derived");

        Assert.True(TypeSymbols.Matches(baseType, baseType));
        Assert.False(TypeSymbols.Matches(derived, baseType));
        Assert.False(TypeSymbols.Matches(baseType, null));
        Assert.False(TypeSymbols.Matches(null, baseType));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void MatchesOriginalDefinition_ComparesTheGenericDefinition()
    {
        var (compilation, members) = Compile();
        var holder = compilation.GetTypeByMetadataName("Holder`1");
        var closed = FieldType(members, "Closed");

        Assert.True(TypeSymbols.MatchesOriginalDefinition(closed, holder));
        Assert.True(TypeSymbols.MatchesOriginalDefinition(holder, holder));
        Assert.False(TypeSymbols.MatchesOriginalDefinition(FieldType(members, "Numbers"), holder));
        Assert.False(TypeSymbols.MatchesOriginalDefinition(closed, null));
        Assert.False(TypeSymbols.MatchesOriginalDefinition(null, holder));
    }
}

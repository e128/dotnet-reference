using System.Linq;
using E128.Analyzers.Testing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class XunitTestHelperBranchCoverageTests
{
    private const string FrameworkStubs = """
        namespace Xunit
        {
            public sealed class FactAttribute : System.Attribute { }
            public sealed class TheoryAttribute : System.Attribute { }
            public static class Assert { }
        }

        namespace Other
        {
            public sealed class FactAttribute : System.Attribute { }
        }

        namespace NUnit.Framework
        {
            public sealed class TestAttribute : System.Attribute { }
            public sealed class TestCaseAttribute : System.Attribute { }
        }

        namespace Microsoft.VisualStudio.TestTools.UnitTesting
        {
            public sealed class TestMethodAttribute : System.Attribute { }
            public sealed class DataTestMethodAttribute : System.Attribute { }
        }

        """;

    private static CSharpCompilation Compile(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(code, cancellationToken: TestContext.Current.CancellationToken);
        return CSharpCompilation.Create(
            "XunitProbe",
            [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
    }

    private static MethodDeclarationSyntax FirstMethod(Compilation compilation)
    {
        var tree = compilation.SyntaxTrees.Single();
        return tree.GetRoot(TestContext.Current.CancellationToken).DescendantNodes().OfType<MethodDeclarationSyntax>().First();
    }

    [Fact]
    [Trait("Category", "CI")]
    public void IsXunitTestMethod_ReadsTheAttributeSymbols()
    {
        var compilation = Compile(FrameworkStubs + "class C { [Xunit.Fact] void A() { } [Xunit.Theory] void B() { } [Other.Fact] void D() { } void E() { } [System.Obsolete] void F() { } }");
        var type = compilation.GetTypeByMetadataName("C")!;

        Assert.True(XunitTestHelper.IsXunitTestMethod((IMethodSymbol)type.GetMembers("A").Single()));
        Assert.True(XunitTestHelper.IsXunitTestMethod((IMethodSymbol)type.GetMembers("B").Single()));
        Assert.False(XunitTestHelper.IsXunitTestMethod((IMethodSymbol)type.GetMembers("D").Single()));
        Assert.False(XunitTestHelper.IsXunitTestMethod((IMethodSymbol)type.GetMembers("E").Single()));
        Assert.False(XunitTestHelper.IsXunitTestMethod((IMethodSymbol)type.GetMembers("F").Single()));
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("[Fact]", true)]
    [InlineData("[Theory]", true)]
    [InlineData("[FactAttribute]", true)]
    [InlineData("[TheoryAttribute]", true)]
    [InlineData("[Xunit.Fact]", true)]
    [InlineData("[global::Xunit.Theory]", true)]
    [InlineData("[System.Obsolete, Fact]", true)]
    [InlineData("[Obsolete]", false)]
    [InlineData("", false)]
    public void GetFactOrTheoryAttribute_ReadsTheLastNameSegment(string attributes, bool expected)
    {
        var method = FirstMethod(Compile("class C { " + attributes + " void M() { } }"));
        Assert.Equal(expected, XunitTestHelper.GetFactOrTheoryAttribute(method) is not null);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void CountDataRows_CountsInlineDataAttributes()
    {
        var method = FirstMethod(Compile("class C { [Theory] [InlineData(1)] [InlineData(2)] [Obsolete] void M(int x) { } }"));
        Assert.Equal(2, XunitTestHelper.CountDataRows(method));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void IsFactOrTheoryAttribute_ChecksTheNameAndTheBoundType()
    {
        var compilation = Compile(FrameworkStubs + "class C { [Xunit.Fact] void A() { } [Other.Fact] void B() { } [Missing.Fact] void D() { } }");
        var model = compilation.GetSemanticModel(compilation.SyntaxTrees.Single());
        var attributes = compilation.SyntaxTrees.Single().GetRoot(TestContext.Current.CancellationToken)
            .DescendantNodes().OfType<AttributeSyntax>().ToArray();

        Assert.True(XunitTestHelper.IsFactOrTheoryAttribute(attributes[0], model, TestContext.Current.CancellationToken));
        Assert.False(XunitTestHelper.IsFactOrTheoryAttribute(attributes[1], model, TestContext.Current.CancellationToken));
        Assert.False(XunitTestHelper.IsFactOrTheoryAttribute(attributes[2], model, TestContext.Current.CancellationToken));
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("[Xunit.Fact]", true)]
    [InlineData("[NUnit.Framework.Test]", true)]
    [InlineData("[NUnit.Framework.TestCase]", true)]
    [InlineData("[Microsoft.VisualStudio.TestTools.UnitTesting.TestMethod]", true)]
    [InlineData("[Microsoft.VisualStudio.TestTools.UnitTesting.DataTestMethod]", true)]
    [InlineData("[System.Obsolete]", false)]
    [InlineData("", false)]
    public void IsTestCompilation_NeedsAFrameworkAndATestMethod(string attribute, bool expected)
    {
        var compilation = Compile(FrameworkStubs + "class C { " + attribute + " void M() { } }");
        Assert.Equal(expected, XunitTestHelper.IsTestCompilation(compilation, TestContext.Current.CancellationToken));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void IsTestCompilation_IsFalseWithoutAFrameworkType()
    {
        var compilation = Compile("class C { [Fact] void M() { } }");
        Assert.False(XunitTestHelper.IsTestCompilation(compilation, TestContext.Current.CancellationToken));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void IsAssertType_RequiresTheXunitNamespace()
    {
        var compilation = Compile(FrameworkStubs + "namespace Other { public static class Assert { } }");

        Assert.True(XunitTestHelper.IsAssertType(compilation.GetTypeByMetadataName("Xunit.Assert")));
        Assert.False(XunitTestHelper.IsAssertType(compilation.GetTypeByMetadataName("Other.Assert")));
        Assert.False(XunitTestHelper.IsAssertType(compilation.GetTypeByMetadataName("Xunit.FactAttribute")));
        Assert.False(XunitTestHelper.IsAssertType(null));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void IsXunitNamespace_IsFalseForNullAndForOtherNamespaces()
    {
        var compilation = Compile(FrameworkStubs);

        Assert.False(XunitTestHelper.IsXunitNamespace(null));
        Assert.False(XunitTestHelper.IsXunitNamespace(compilation.GetTypeByMetadataName("Other.FactAttribute")!.ContainingNamespace));
        Assert.True(XunitTestHelper.IsXunitNamespace(compilation.GetTypeByMetadataName("Xunit.FactAttribute")!.ContainingNamespace));
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("Fact", "Fact")]
    [InlineData("Xunit.Fact", "Fact")]
    [InlineData("global::Xunit.Fact", "Fact")]
    public void SimpleName_ReturnsTheLastSegment(string text, string expected)
    {
        var name = SyntaxFactory.ParseName(text);
        Assert.Equal(expected, XunitTestHelper.SimpleName(name));
    }
}

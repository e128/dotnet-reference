using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class NamespacesBranchCoverageTests
{
    private static INamespaceSymbol Namespace(string dotted)
    {
        var tree = CSharpSyntaxTree.ParseText(
            "namespace " + dotted + " { class Probe { } }",
            cancellationToken: TestContext.Current.CancellationToken);
        var compilation = CSharpCompilation.Create(
            "NamespaceProbe",
            [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        return compilation.GetTypeByMetadataName(dotted + ".Probe")!.ContainingNamespace;
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("System.Net.Http", "System.Net.Http", true)]
    [InlineData("System", "System", true)]
    [InlineData("System.Net", "System", false)]
    [InlineData("System.Net", "Net", false)]
    [InlineData("System.Net", "A.System.Net", false)]
    [InlineData("System.Net", "System.Net.Http", false)]
    [InlineData("System.Net", ".Net", false)]
    [InlineData("System.Net", "System.Nex", false)]
    [InlineData("System.Net", "Systen.Net", false)]
    [InlineData("System.Net", "", false)]
    public void Equals_ComparesTheWholeChain(string actual, string dotted, bool expected)
    {
        Assert.Equal(expected, Namespaces.Equals(Namespace(actual), dotted));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void Equals_IsFalseForANullNamespace()
    {
        Assert.False(Namespaces.Equals(null, "System"));
    }
}

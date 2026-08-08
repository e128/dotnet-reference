using Xunit;

namespace E128.Analyzers.Tests;

public sealed class PragmaFileScopedNamespaceCodeFixTests
{
    [Fact]
    [Trait("Category", "CI")]
    public void CodeFix_MovesPragma_BelowNamespaceDeclaration()
        => Assert.Fail("Not implemented — see Verifiable Behaviors in plan.md");
}

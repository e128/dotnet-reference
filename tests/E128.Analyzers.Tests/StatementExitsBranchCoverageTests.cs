using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class StatementExitsBranchCoverageTests
{
    [Theory]
    [Trait("Category", "CI")]
    [InlineData("return;", true)]
    [InlineData("throw new System.Exception();", true)]
    [InlineData("continue;", true)]
    [InlineData("break;", true)]
    [InlineData("{ }", false)]
    [InlineData("{ Use(); }", false)]
    [InlineData("{ Use(); return; }", true)]
    [InlineData("{ { return; } }", true)]
    [InlineData("{ return; Use(); }", false)]
    [InlineData("if (b) return;", false)]
    [InlineData("if (b) return; else Use();", false)]
    [InlineData("if (b) Use(); else return;", false)]
    [InlineData("if (b) return; else break;", true)]
    [InlineData("if (b) { return; } else if (c) { continue; } else { throw new System.Exception(); }", true)]
    [InlineData("if (b) { return; } else if (c) { continue; }", false)]
    [InlineData("Use();", false)]
    public void EndsInExit_ReadsBlocksAndIfBranches(string statement, bool expected)
    {
        Assert.Equal(expected, StatementExits.EndsInExit(SyntaxFactory.ParseStatement(statement)));
    }
}

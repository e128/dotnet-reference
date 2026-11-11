using System.Threading.Tasks;
using E128.Analyzers.Style;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class EmptyStringLiteralAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task VariableDeclaration_EmptyLiteral_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<EmptyStringLiteralAnalyzer>("""
                           class C
                           {
                               void M()
                               {
                                   var x = {|E128002:""|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ReturnStatement_EmptyLiteral_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<EmptyStringLiteralAnalyzer>("""
                           class C
                           {
                               string M() => {|E128002:""|};
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AttributeArgument_NoFire()
    {
        return AnalyzerTestRunner.VerifyAsync<EmptyStringLiteralAnalyzer>("""
                           class C
                           {
                               [System.Obsolete("")]
                               void M() { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConstField_NoFire()
    {
        return AnalyzerTestRunner.VerifyAsync<EmptyStringLiteralAnalyzer>("""
                           class C
                           {
                               private const string K = "";
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DefaultParameter_NoFire()
    {
        return AnalyzerTestRunner.VerifyAsync<EmptyStringLiteralAnalyzer>("""
                           class C
                           {
                               void M(string s = "") { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SwitchCaseLabel_NoFire()
    {
        return AnalyzerTestRunner.VerifyAsync<EmptyStringLiteralAnalyzer>("""
                           class C
                           {
                               int M(string s)
                               {
                                   switch (s)
                                   {
                                       case "":
                                           return 0;
                                       default:
                                           return 1;
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Pattern_NoFire()
    {
        // string.Empty is not a constant, so it cannot replace a pattern.
        return AnalyzerTestRunner.VerifyAsync<EmptyStringLiteralAnalyzer>("""
                           class C
                           {
                               bool M(string s) => s is "";
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SwitchExpressionArm_NoFire()
    {
        return AnalyzerTestRunner.VerifyAsync<EmptyStringLiteralAnalyzer>("""
                           class C
                           {
                               int M(string s) => s switch
                               {
                                   "" => 0,
                                   _ => 1
                               };
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NonEmptyLiteral_NoFire()
    {
        return AnalyzerTestRunner.VerifyAsync<EmptyStringLiteralAnalyzer>("""
                           class C
                           {
                               void M()
                               {
                                   var x = "hello";
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task GotoCaseLiteral_NoFire()
    {
        return AnalyzerTestRunner.VerifyAsync<EmptyStringLiteralAnalyzer>("""
                           class C
                           {
                               void M(string s)
                               {
                                   switch (s)
                                   {
                                       case "a":
                                           goto case "";
                                       case "":
                                           break;
                                   }
                               }
                           }
                           """);
    }
}

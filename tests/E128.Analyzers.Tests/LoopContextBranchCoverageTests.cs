using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class LoopContextBranchCoverageTests
{
    private const string Prelude = """
        #nullable enable
        using System;
        using System.Threading;
        using System.Threading.Tasks;

        class Other
        {
            public static void Sleep(int ms) { }
            public static Task Delay(int ms) { return Task.CompletedTask; }
        }

        class C
        {
            int[] values = new int[4];
            int[] items = new int[4];
            (int, int)[] pairs = new (int, int)[4];
            string? name;
            const int Hops = 5;
            void Use(int value) { }

        """;

    private static (SyntaxNode Root, SemanticModel Model) Parse(string members)
    {
        var tree = CSharpSyntaxTree.ParseText(
            Prelude + members + "\n}\n",
            cancellationToken: TestContext.Current.CancellationToken);
        var compilation = CSharpCompilation.Create(
            "LoopContextProbe",
            [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        return (tree.GetRoot(TestContext.Current.CancellationToken), compilation.GetSemanticModel(tree));
    }

    private static T Find<T>(SyntaxNode root, string text)
        where T : SyntaxNode
    {
        return root.DescendantNodes().OfType<T>().First(n => string.Equals(n.ToString(), text, StringComparison.Ordinal));
    }

    private static bool ContainsWait(string bodyStatements)
    {
        var (root, model) = Parse("void M() { while (true) { " + bodyStatements + " } }");
        var body = root.DescendantNodes().OfType<WhileStatementSyntax>().Single().Statement;
        return LoopContext.ContainsWaitCall(body, model, TestContext.Current.CancellationToken);
    }

    private static bool References(string members, string nodeText)
    {
        var (root, model) = Parse(members);
        var node = Find<ExpressionSyntax>(root, nodeText);
        var body = LoopContext.FindEnclosingLoopBody(node);
        Assert.NotNull(body);
        return LoopContext.ReferencesLoopVariable(node, body, model, TestContext.Current.CancellationToken);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void ContainsWaitCall_IsTrueForTaskDelay()
    {
        Assert.True(ContainsWait("Task.Delay(10);"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void ContainsWaitCall_IsTrueForThreadSleep()
    {
        Assert.True(ContainsWait("Thread.Sleep(10);"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void ContainsWaitCall_IsFalseForSameNamedMethodsOnOtherTypes()
    {
        Assert.False(ContainsWait("Other.Sleep(10); Other.Delay(10);"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void ContainsWaitCall_IsFalseForOtherMethodsOnTaskAndThread()
    {
        Assert.False(ContainsWait("Task.Yield(); Thread.Yield(); Use(1);"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void ContainsWaitCall_SkipsCallsThatDoNotBind()
    {
        Assert.False(ContainsWait("Missing(1); this.Missing(2);"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void ReferencesLoopVariable_IsTrueWhenTheNodeCallsAMethod()
    {
        Assert.True(References("void M() { while (true) { Use(values[Compute()]); } } int Compute() { return 1; }", "values[Compute()]"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void ReferencesLoopVariable_IsFalseWhenNothingVaries()
    {
        Assert.False(References("void M() { while (true) { Use(values[0]); } }", "values[0]"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void ReferencesLoopVariable_IsTrueForTheForeachVariable()
    {
        Assert.True(References("void M() { foreach (var item in items) { Use(values[item]); } }", "values[item]"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void ReferencesLoopVariable_IsFalseWhenTheForeachVariableIsNotRead()
    {
        Assert.False(References("void M() { foreach (var item in items) { Use(values[0]); } }", "values[0]"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void ReferencesLoopVariable_IsTrueForADeconstructedForeachVariable()
    {
        Assert.True(References("void M() { foreach (var (a, b) in pairs) { Use(values[a]); } }", "values[a]"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void ReferencesLoopVariable_IsFalseForAForeachDeconstructionThatIsNotRead()
    {
        Assert.False(References("void M() { foreach (var (a, b) in pairs) { Use(values[0]); } }", "values[0]"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void ReferencesLoopVariable_IsTrueForTheForCounter()
    {
        Assert.True(References("void M() { for (var i = 0; i < 3; i++) { Use(values[i]); } }", "values[i]"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void ReferencesLoopVariable_IsFalseForAForLoopWithoutADeclaration()
    {
        Assert.False(References("void M() { for (; ; ) { Use(values[0]); } }", "values[0]"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void ReferencesLoopVariable_IsTrueForALocalDeclaredInTheBody()
    {
        Assert.True(References("void M() { while (true) { var local = 1; Use(values[local]); } }", "values[local]"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void ReferencesLoopVariable_IsTrueForAVariableAssignedInTheBody()
    {
        Assert.True(References("void M() { var j = 0; while (true) { j = 2; Use(values[j]); } }", "values[j]"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void ReferencesLoopVariable_IsFalseWhenTheBodyAssignsAnElement()
    {
        Assert.False(References("void M() { while (true) { values[0] = 1; Use(values[1]); } }", "values[1]"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void ReferencesLoopVariable_IsTrueForAPrefixIncrementInTheBody()
    {
        Assert.True(References("void M() { var j = 0; while (true) { ++j; Use(values[j]); } }", "values[j]"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void ReferencesLoopVariable_IsTrueForAPrefixDecrementInTheBody()
    {
        Assert.True(References("void M() { var j = 0; while (true) { --j; Use(values[j]); } }", "values[j]"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void ReferencesLoopVariable_IsTrueForAPostfixIncrementInTheBody()
    {
        Assert.True(References("void M() { var j = 0; while (true) { j++; Use(values[j]); } }", "values[j]"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void ReferencesLoopVariable_IsTrueForAPostfixDecrementInTheBody()
    {
        Assert.True(References("void M() { var j = 0; while (true) { j--; Use(values[j]); } }", "values[j]"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void ReferencesLoopVariable_IsFalseForUnaryOperatorsThatDoNotWrite()
    {
        Assert.False(References("void M() { var j = 0; while (true) { Use(-j); Use(values[j]); } }", "values[j]"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void ReferencesLoopVariable_IsFalseForTheNullForgivingOperator()
    {
        Assert.False(References("void M() { while (true) { Use(name!.Length); Use(values[name.Length]); } }", "values[name.Length]"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void ReferencesLoopVariable_IsFalseForIncrementsOfMembers()
    {
        Assert.False(References("void M() { while (true) { values[0]++; ++values[1]; Use(values[2]); } }", "values[2]"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void ReferencesLoopVariable_SkipsAssignmentsToUnknownNames()
    {
        Assert.False(References("void M() { while (true) { unknown = 1; Use(values[0]); } }", "values[0]"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void ReferencesLoopVariable_SkipsUnknownNamesInTheNode()
    {
        Assert.False(References("void M() { foreach (var item in items) { Use(values[missing]); } }", "values[missing]"));
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("for (var i = 0; i < 10; i++) { Use(1); }", true)]
    [InlineData("for (var i = 0; i <= 10; i++) { Use(1); }", true)]
    [InlineData("for (var i = 10; 10 >= i; i--) { Use(1); }", true)]
    [InlineData("for (var i = 0; i < Hops; i++) { Use(1); }", true)]
    [InlineData("for (var i = 0; i < Hops * 2; i++) { Use(1); }", true)]
    [InlineData("for (var i = 0; i < items.Length; i++) { Use(1); }", false)]
    [InlineData("for (var i = 0; i != 10; i++) { Use(1); }", false)]
    [InlineData("for (var i = 0; i < 10 && i < 20; i++) { Use(1); }", false)]
    [InlineData("for (; ; ) { Use(1); }", false)]
    [InlineData("while (true) { Use(1); }", false)]
    [InlineData("foreach (var item in items) { Use(1); }", false)]
    [InlineData("for (var i = 0; i < 10; i++) { Action a = () => Use(1); }", false)]
    public void HasConstantBound_ReadsTheNearestForCondition(string statement, bool expected)
    {
        var (root, model) = Parse("void M() { " + statement + " }");
        var node = Find<InvocationExpressionSyntax>(root, "Use(1)");
        Assert.Equal(expected, LoopContext.HasConstantBound(node, model, TestContext.Current.CancellationToken));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void FindEnclosingLoop_ReturnsTheLoopStatementForABodyNode()
    {
        var (root, _) = Parse("void M() { while (true) { Use(1); } }");
        var loop = LoopContext.FindEnclosingLoop(Find<InvocationExpressionSyntax>(root, "Use(1)"));
        _ = Assert.IsType<WhileStatementSyntax>(loop);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("for (var i = Use(1); i < 3; i++) { }")]
    [InlineData("for (var i = 0; i < Use(1); i++) { }")]
    [InlineData("for (var i = 0; i < 3; i += Use(1)) { }")]
    [InlineData("foreach (var item in Make(Use(1))) { }")]
    [InlineData("while (Use(1) > 0) { }")]
    [InlineData("do { } while (Use(1) > 0);")]
    public void FindEnclosingLoopBody_IsNullForHeaderNodes(string statement)
    {
        var (root, _) = Parse("void M() { " + statement + " } int Use(int v) { return v; } int[] Make(int v) { return items; }");
        Assert.Null(LoopContext.FindEnclosingLoopBody(Find<InvocationExpressionSyntax>(root, "Use(1)")));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void FindEnclosingLoopBody_FallsBackToTheOuterLoopForAHeaderNode()
    {
        var (root, _) = Parse(
            "void M() { while (true) { for (var i = Use(1); i < 3; i++) { } } } int Use(int v) { return v; }");
        var body = LoopContext.FindEnclosingLoopBody(Find<InvocationExpressionSyntax>(root, "Use(1)"));
        var outer = root.DescendantNodes().OfType<WhileStatementSyntax>().Single();
        Assert.Same(outer.Statement, body);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void FindEnclosingLoopBody_IsNullInsideALambda()
    {
        var (root, _) = Parse("void M() { while (true) { Action a = () => Use(1); } }");
        Assert.Null(LoopContext.FindEnclosingLoopBody(Find<InvocationExpressionSyntax>(root, "Use(1)")));
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("for (var i = 0; i < Use(1); i++) { }", true)]
    [InlineData("for (var i = 0; i < 3; i += Use(1)) { }", true)]
    [InlineData("while (Use(1) > 0) { }", true)]
    [InlineData("do { } while (Use(1) > 0);", true)]
    [InlineData("for (var i = Use(1); i < 3; i++) { }", false)]
    [InlineData("foreach (var item in Make(Use(1))) { }", false)]
    [InlineData("while (true) { Use(1); }", true)]
    [InlineData("Use(1);", false)]
    public void IsInsideLoopBody_CountsConditionsAndIncrementors(string statement, bool expected)
    {
        var (root, _) = Parse("void M() { " + statement + " } int Use(int v) { return v; } int[] Make(int v) { return items; }");
        Assert.Equal(expected, LoopContext.IsInsideLoopBody(Find<InvocationExpressionSyntax>(root, "Use(1)")));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void IsInsideLoopBody_IsTrueInsideALambdaInTheLoop()
    {
        var (root, _) = Parse("void M() { while (true) { Action a = () => Use(1); } }");
        Assert.True(LoopContext.IsInsideLoopBody(Find<InvocationExpressionSyntax>(root, "Use(1)")));
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("for (var i = Use(1); i < 3; i++) { }", true)]
    [InlineData("foreach (var item in Make(Use(1))) { }", true)]
    [InlineData("while (true) { Use(1); }", true)]
    [InlineData("Use(1);", false)]
    public void IsWithinAnyLoop_IncludesLoopHeaders(string statement, bool expected)
    {
        var (root, _) = Parse("void M() { " + statement + " } int Use(int v) { return v; } int[] Make(int v) { return items; }");
        Assert.Equal(expected, LoopContext.IsWithinAnyLoop(Find<InvocationExpressionSyntax>(root, "Use(1)")));
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("public C() { Use(1); }")]
    [InlineData("~C() { Use(1); }")]
    [InlineData("public static C operator +(C a, C b) { Use(1); return a; }")]
    [InlineData("public static implicit operator int(C a) { Use(1); return 0; }")]
    [InlineData("int P { get { Use(1); return 0; } }")]
    [InlineData("void M() { Use(1); }")]
    [InlineData("void M() { while (true) { void Local() { Use(1); } } }")]
    public void IsWithinAnyLoop_StopsAtDeclarationBoundaries(string members)
    {
        var (root, _) = Parse(members + " ");
        Assert.False(LoopContext.IsWithinAnyLoop(Find<InvocationExpressionSyntax>(root, "Use(1)")));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void IsWithinAnyLoop_SeesALoopAroundALambda()
    {
        var (root, _) = Parse("void M() { while (true) { Action a = () => Use(1); } }");
        Assert.True(LoopContext.IsWithinAnyLoop(Find<InvocationExpressionSyntax>(root, "Use(1)")));
    }
}

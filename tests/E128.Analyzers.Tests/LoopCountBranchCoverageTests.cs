using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class LoopCountBranchCoverageTests
{
    private const string Prelude = """
        using System;
        using System.Collections.Generic;

        class WithStaticCount
        {
            public static int Count => 1;
            public IEnumerator<int> GetEnumerator() { return null!; }
        }

        class WithLongCount
        {
            public long Count => 1;
            public IEnumerator<int> GetEnumerator() { return null!; }
        }

        class WithPrivateCount
        {
            int Count => 1;
            public IEnumerator<int> GetEnumerator() { return null!; }
        }

        class WithInternalCount
        {
            internal int Count => 1;
            public IEnumerator<int> GetEnumerator() { return null!; }
        }

        class WithLength
        {
            public int Length => 1;
            public IEnumerator<int> GetEnumerator() { return null!; }
        }

        class Derived : WithLength
        {
        }

        class C
        {
            int[] array = new int[2];
            List<int> list = new List<int>();
            string text = "";
            IEnumerable<int> sequence = new int[2];
            ICollection<int> collection = new List<int>();
            WithStaticCount staticCount = new WithStaticCount();
            WithLongCount longCount = new WithLongCount();
            WithPrivateCount privateCount = new WithPrivateCount();
            WithInternalCount internalCount = new WithInternalCount();
            Derived derived = new Derived();
            C next = null!;
            List<int> Make() { return list; }
            void Use(int value) { }

        """;

    private static (SyntaxNode Root, SemanticModel Model) Parse(string members)
    {
        var tree = CSharpSyntaxTree.ParseText(
            Prelude + members + "\n}\n",
            cancellationToken: TestContext.Current.CancellationToken);
        var compilation = CSharpCompilation.Create(
            "LoopCountProbe",
            [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        return (tree.GetRoot(TestContext.Current.CancellationToken), compilation.GetSemanticModel(tree));
    }

    private static string? CountOf(string loop)
    {
        var (root, model) = Parse("void M(int n) { " + loop + " }");
        var statement = root.DescendantNodes().OfType<StatementSyntax>()
            .First(static s => s is ForStatementSyntax or CommonForEachStatementSyntax or WhileStatementSyntax);
        return LoopCount.TryGetCount(statement, model, TestContext.Current.CancellationToken);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("foreach (var x in array) { }", "array.Length")]
    [InlineData("foreach (var x in list) { }", "list.Count")]
    [InlineData("foreach (var x in text) { }", "text.Length")]
    [InlineData("foreach (var x in collection) { }", "collection.Count")]
    [InlineData("foreach (var x in derived) { }", "derived.Length")]
    [InlineData("foreach (var x in next.list) { }", "next.list.Count")]
    [InlineData("foreach (var x in internalCount) { }", "internalCount.Count")]
    [InlineData("foreach (var (a, b) in new (int, int)[0]) { }", null)]
    [InlineData("foreach (var x in sequence) { }", null)]
    [InlineData("foreach (var x in Make()) { }", null)]
    [InlineData("foreach (var x in staticCount) { }", null)]
    [InlineData("foreach (var x in longCount) { }", null)]
    [InlineData("foreach (var x in privateCount) { }", null)]
    [InlineData("foreach (var x in missing) { }", null)]
    [InlineData("for (var i = 0; i < list.Count; i++) { }", "list.Count")]
    [InlineData("for (var i = 0; i < array.Length; i++) { }", "array.Length")]
    [InlineData("for (var i = 0; i < next.list.Count; i++) { }", "next.list.Count")]
    [InlineData("for (var i = 0; i < n; i++) { }", null)]
    [InlineData("for (var i = 0; i < Make().Count; i++) { }", null)]
    [InlineData("for (var i = 0; i < list.Capacity; i++) { }", null)]
    [InlineData("for (var i = 0; i <= list.Count; i++) { }", null)]
    [InlineData("for (; ; ) { }", null)]
    [InlineData("while (true) { }", null)]
    public void TryGetCount_ReadsTheLoopSource(string loop, string? expected)
    {
        Assert.Equal(expected, CountOf(loop));
    }

    private static (StatementSyntax? Result, StatementSyntax? Next) FillLoop(string statements, string methodName = "Add")
    {
        var (root, model) = Parse("void M() { " + statements + " }");
        var declaration = root.DescendantNodes().OfType<LocalDeclarationStatementSyntax>().First();
        var receiver = model.GetDeclaredSymbol(declaration.Declaration.Variables[0], TestContext.Current.CancellationToken)!;
        var result = LoopCount.TryGetNextFillLoop(
            declaration,
            receiver,
            [methodName, "Insert"],
            model,
            TestContext.Current.CancellationToken);
        var block = (BlockSyntax)declaration.Parent!;
        var index = block.Statements.IndexOf(declaration);
        return (result, index + 1 < block.Statements.Count ? block.Statements[index + 1] : null);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void TryGetNextFillLoop_ReturnsTheLoopThatCallsTheMethod()
    {
        var (result, next) = FillLoop("var target = new List<int>(); for (var i = 0; i < 3; i++) { target.Add(i); }");
        Assert.Same(next, result);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void TryGetNextFillLoop_AcceptsAnyListedMethodName()
    {
        var (result, next) = FillLoop("var target = new List<int>(); foreach (var i in list) { target.Insert(0, i); }");
        Assert.Same(next, result);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void TryGetNextFillLoop_IsNullWhenTheLoopMayStopEarly()
    {
        var (result, _) = FillLoop("var target = new List<int>(); foreach (var i in list) { target.Add(i); break; }");
        Assert.Null(result);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void TryGetNextFillLoop_IsNullWhenTheLoopCallsAnotherMethod()
    {
        var (result, _) = FillLoop("var target = new List<int>(); foreach (var i in list) { target.Clear(); }");
        Assert.Null(result);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void TryGetNextFillLoop_IsNullForTheLastStatementInTheBlock()
    {
        var (result, next) = FillLoop("var target = new List<int>();");
        Assert.Null(next);
        Assert.Null(result);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void TryGetNextFillLoop_IsNullWhenTheDeclarationIsNotInABlock()
    {
        var (root, model) = Parse("void M(int k) { switch (k) { case 1: var target = new List<int>(); foreach (var i in list) { target.Add(i); } break; } }");
        var declaration = root.DescendantNodes().OfType<LocalDeclarationStatementSyntax>().First();
        var receiver = model.GetDeclaredSymbol(declaration.Declaration.Variables[0], TestContext.Current.CancellationToken)!;

        var result = LoopCount.TryGetNextFillLoop(declaration, receiver, ["Add"], model, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("target.Add(1);", true)]
    [InlineData("other.Add(1);", false)]
    [InlineData("this.list.Add(1);", false)]
    [InlineData("target.Clear();", false)]
    [InlineData("Action a = () => target.Add(1);", false)]
    [InlineData("void Local() { target.Add(1); }", false)]
    [InlineData("Use(target.Count);", false)]
    [InlineData("Make().Add(1);", false)]
    [InlineData("missing.Add(1);", false)]
    public void ContainsCall_FindsOnlyDirectCallsOnTheReceiver(string body, bool expected)
    {
        var (root, model) = Parse("void M() { var target = new List<int>(); var other = new List<int>(); while (true) { " + body + " } }");
        var declaration = root.DescendantNodes().OfType<LocalDeclarationStatementSyntax>().First();
        var receiver = model.GetDeclaredSymbol(declaration.Declaration.Variables[0], TestContext.Current.CancellationToken)!;
        var loop = root.DescendantNodes().OfType<WhileStatementSyntax>().Single();

        Assert.Equal(expected, LoopCount.ContainsCall(loop, receiver, "Add", model, TestContext.Current.CancellationToken));
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("while (true) { break; }", true)]
    [InlineData("while (true) { if (n > 0) { break; } }", true)]
    [InlineData("while (true) { for (;;) { break; } }", false)]
    [InlineData("while (true) { foreach (var x in list) { break; } }", false)]
    [InlineData("while (true) { while (n > 0) { break; } }", false)]
    [InlineData("while (true) { do { break; } while (n > 0); }", false)]
    [InlineData("while (true) { switch (n) { case 1: break; } }", false)]
    [InlineData("while (true) { switch (n) { case 1: break; } break; }", true)]
    [InlineData("while (true) { Use(n); }", false)]
    public void HasDirectBreak_IgnoresBreaksOfNestedConstructs(string loop, bool expected)
    {
        var (root, _) = Parse("void M(int n) { " + loop + " }");
        var statement = root.DescendantNodes().OfType<WhileStatementSyntax>().First();
        Assert.Equal(expected, LoopCount.HasDirectBreak(statement));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void TryGetNextFillLoop_HandlesAnEmptyMethodNameList()
    {
        var (root, model) = Parse("void M() { var target = new List<int>(); foreach (var i in list) { target.Add(i); } }");
        var declaration = root.DescendantNodes().OfType<LocalDeclarationStatementSyntax>().First();
        var receiver = model.GetDeclaredSymbol(declaration.Declaration.Variables[0], TestContext.Current.CancellationToken)!;

        Assert.Null(LoopCount.TryGetNextFillLoop(declaration, receiver, [], model, TestContext.Current.CancellationToken));
    }
}

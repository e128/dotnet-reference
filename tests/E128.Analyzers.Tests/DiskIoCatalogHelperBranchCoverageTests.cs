using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;
using static E128.Analyzers.Reliability.DiskIoCatalog;

namespace E128.Analyzers.Tests;

public sealed class DiskIoCatalogHelperBranchCoverageTests
{
    private static ExpressionSyntax Expr(string text)
    {
        return SyntaxFactory.ParseExpression(text);
    }

    private static MethodDeclarationSyntax Method(string body)
    {
        var tree = CSharpSyntaxTree.ParseText("class C { void M() {\n" + body + "\n} }", cancellationToken: TestContext.Current.CancellationToken);
        return tree.GetRoot(TestContext.Current.CancellationToken).DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
    }

    private static StatementSyntax Statement(SyntaxNode root, string text)
    {
        return root.DescendantNodes().OfType<StatementSyntax>().First(s => string.Equals(s.ToString(), text, StringComparison.Ordinal));
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("WriteAllText", "Text")]
    [InlineData("ReadAllBytes", "Bytes")]
    [InlineData("AppendAllLines", "Lines")]
    [InlineData("OpenWrite", "Stream")]
    [InlineData("AppendText", "Writer")]
    [InlineData("Delete", "Unknown")]
    public void FileMethodKind_MapsTheMethodNameToItsKind(string name, string expected)
    {
        Assert.Equal(expected, FileMethodKind(name).ToString());
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("WriteAllText", true, false)]
    [InlineData("AppendAllLinesAsync", true, false)]
    [InlineData("WriteAllBytes", true, false)]
    [InlineData("ReadAllText", false, true)]
    [InlineData("ReadAllLines", false, true)]
    [InlineData("Create", false, false)]
    [InlineData("OpenRead", false, false)]
    [InlineData("Delete", false, false)]
    public void ValueMethods_SplitIntoWriteAndReadByPrefix(string name, bool isWrite, bool isRead)
    {
        Assert.Equal(isWrite, IsFileWriteValueMethod(name));
        Assert.Equal(isRead, IsFileReadValueMethod(name));
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("Create", true, false)]
    [InlineData("OpenWrite", true, false)]
    [InlineData("CreateText", true, false)]
    [InlineData("AppendText", true, false)]
    [InlineData("OpenRead", false, true)]
    [InlineData("OpenText", false, true)]
    [InlineData("WriteAllText", false, false)]
    [InlineData("Delete", false, false)]
    public void Factories_SplitIntoWriteAndReadByName(string name, bool isWrite, bool isRead)
    {
        Assert.Equal(isWrite, IsFileWriteFactory(name));
        Assert.Equal(isRead, IsFileReadFactory(name));
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("SaveToDisk", true)]
    [InlineData("exportTOFILE", true)]
    [InlineData("SaveTo", true)]
    [InlineData("WriteTo", true)]
    [InlineData("DownloadTo", true)]
    [InlineData("Compute", false)]
    public void IsOpaqueWriteMethodName_MatchesWriteHintsIgnoringCase(string name, bool expected)
    {
        Assert.Equal(expected, IsOpaqueWriteMethodName(name));
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData(0, "text")]
    [InlineData(1, "bytes")]
    [InlineData(2, "lines")]
    [InlineData(3, "stream")]
    [InlineData(4, "writer")]
    [InlineData(5, "reader")]
    [InlineData(6, "binary")]
    [InlineData(7, "value")]
    [InlineData(99, "value")]
    public void KindDescription_NamesEachKind(int kind, string expected)
    {
        Assert.Equal(expected, KindDescription((IoKind)kind));
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("StreamWriter", "StreamWriter")]
    [InlineData("System.IO.StreamReader", "StreamReader")]
    [InlineData("List<int>", "List")]
    [InlineData("int", null)]
    public void ExtractCtorTypeName_ReturnsTheSimpleTypeName(string type, string? expected)
    {
        Assert.Equal(expected, ExtractCtorTypeName(SyntaxFactory.ParseTypeName(type)));
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("FileMode.Open", "FileAccess.Write", true)]
    [InlineData("FileMode.Create", "FileAccess.Read", false)]
    [InlineData("FileMode.Open", "FileAccess.ReadWrite", true)]
    [InlineData("FileMode.Create", "FileAccess.Unknown", true)]
    [InlineData("FileMode.Open", "FileAccess.Unknown", false)]
    [InlineData("FileMode.Append", null, true)]
    [InlineData("FileMode.Open", null, false)]
    [InlineData("mode", "access", false)]
    [InlineData(null, null, false)]
    public void IsFileStreamWriteIntent_PrefersAccessAndFallsBackToMode(string? mode, string? access, bool expected)
    {
        var modeExpr = mode is null ? null : Expr(mode);
        var accessExpr = access is null ? null : Expr(access);

        Assert.Equal(expected, IsFileStreamWriteIntent(modeExpr, accessExpr));
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("File.Create(path)", "path")]
    [InlineData("File.Create(path, 4096)", "path")]
    [InlineData("File.Create()", null)]
    [InlineData("Other.Create(path)", null)]
    [InlineData("Create(path)", null)]
    [InlineData("a.File.Create(path)", null)]
    [InlineData("path", null)]
    public void ExtractPathFromStreamArg_ReadsTheFileCreatePath(string arg, string? expected)
    {
        var path = ExtractPathFromStreamArg(Expr(arg));

        Assert.Equal(expected, path?.ToString());
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("path", "Ident:path")]
    [InlineData("info.FullName", "Member:info.FullName")]
    [InlineData("a.b.Name", "OpaqueWriteRoot:a")]
    [InlineData("Make().Inner.Name", "Expr:Make().Inner.Name")]
    [InlineData("1 + 2", "Expr:1 + 2")]
    public void Normalize_BuildsAKeyForEachPathShape(string expression, string expected)
    {
        Assert.Equal(expected, Normalize(Expr(expression)));
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("Member:info.FullName", "Member:info.FullName|FileInfo:info")]
    [InlineData("FileInfo:info", "FileInfo:info|Member:info.FullName")]
    [InlineData("Member:info.Name", "Member:info.Name")]
    [InlineData("Ident:p", "Ident:p")]
    public void CompatibleKeys_ExpandsFileInfoAndFullNameKeys(string key, string expected)
    {
        Assert.Equal(expected, string.Join('|', CompatibleKeys(key)));
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("Ident:p", "Ident:p", true)]
    [InlineData("Member:info.FullName", "FileInfo:info", true)]
    [InlineData("FileInfo:info", "Member:info.FullName", true)]
    [InlineData("OpaqueWriteResult:r", "OpaqueWriteRoot:r", true)]
    [InlineData("OpaqueWriteRoot:r", "OpaqueWriteResult:r", true)]
    [InlineData("Ident:p", "Ident:q", false)]
    [InlineData("OpaqueWriteResult:r", "Ident:r", false)]
    public void KeysOverlap_MatchesEqualCompatibleAndOpaqueKeys(string a, string b, bool expected)
    {
        Assert.Equal(expected, KeysOverlap(a, b));
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("OpaqueWriteResult:r", "OpaqueWriteRoot:r", true)]
    [InlineData("OpaqueWriteResult:r", "OpaqueWriteRoot:other", false)]
    [InlineData("OpaqueWriteResult:r", "Member:r.Path", true)]
    [InlineData("OpaqueWriteResult:r", "Member:rr.Path", false)]
    [InlineData("OpaqueWriteResult:r", "Ident:r", false)]
    [InlineData("Ident:r", "OpaqueWriteRoot:r", false)]
    public void OpaqueWriteResultOverlap_ComparesTheResultVariableToTheReadKey(string writeKey, string readKey, bool expected)
    {
        Assert.Equal(expected, OpaqueWriteResultOverlap(writeKey, readKey));
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("Ident:p", "p")]
    [InlineData("Member:info.FullName", "info")]
    [InlineData("Member:info", null)]
    [InlineData("FileInfo:info", "info")]
    [InlineData("OpaqueWriteResult:r", "r")]
    [InlineData("OpaqueWriteRoot:root", "root")]
    [InlineData("Expr:1 + 2", null)]
    public void ExtractIdentifier_ReadsTheNameFromEachKeyKind(string key, string? expected)
    {
        Assert.Equal(expected, ExtractIdentifier(key));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void FindFactoryIndex_ReturnsThePositionOrMinusOne()
    {
        var first = Expr("File.Create(a)");
        var second = Expr("File.Create(b)");
        var writes = new List<WriteOp>
        {
            new(first, first, "Ident:a", null, IoKind.Stream, false),
            new(second, second, "Ident:b", null, IoKind.Stream, false)
        };

        Assert.Equal(0, FindFactoryIndex(writes, first));
        Assert.Equal(1, FindFactoryIndex(writes, second));
        Assert.Equal(-1, FindFactoryIndex(writes, Expr("File.Create(c)")));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void TrackWriterVariable_MapsOnlyVariablesInitializedFromAKnownFactory()
    {
        var method = Method("""
            StreamWriter declared;
            var known = File.CreateText(a);
            var unknown = File.CreateText(b);
            """);
        var declarations = method.DescendantNodes().OfType<VariableDeclarationSyntax>().ToArray();
        var factory = declarations[1].Variables[0].Initializer!.Value;
        var writes = new List<WriteOp> { new(factory, factory, "Ident:a", null, IoKind.Writer, false) };
        var map = new Dictionary<string, int>(StringComparer.Ordinal);

        TrackWriterVariable(declarations[0], writes, map);
        TrackWriterVariable(declarations[1], writes, map);
        TrackWriterVariable(declarations[2], writes, map);

        Assert.Equal(new KeyValuePair<string, int>("known", 0), Assert.Single(map));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void TryTrackOpaqueWriteResult_RecordsOnlyOpaqueWriteInvocations()
    {
        var method = Method("""
            int none;
            var plain = value;
            var local = Compute();
            var other = client.Compute();
            var sync = client.SaveToDisk(a);
            var asynchronous = await client.DownloadToFile(b);
            """);
        var writes = new List<WriteOp>();

        foreach (var declaration in method.DescendantNodes().OfType<VariableDeclarationSyntax>())
        {
            TryTrackOpaqueWriteResult(declaration, writes);
        }

        Assert.Equal("OpaqueWriteResult:sync|OpaqueWriteResult:asynchronous", string.Join('|', writes.Select(static w => w.PathKey)));
        Assert.All(writes, static w => Assert.Equal(IoKind.Unknown, w.Kind));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void WriteLinearlyPrecedesRead_IsTrueForSiblingStatementsInOrder()
    {
        var method = Method("write(); read();");

        Assert.True(WriteLinearlyPrecedesRead(Statement(method, "write();"), Statement(method, "read();")));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void WriteLinearlyPrecedesRead_IsFalseWhenTheReadComesFirst()
    {
        var method = Method("read(); write();");

        Assert.False(WriteLinearlyPrecedesRead(Statement(method, "write();"), Statement(method, "read();")));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void WriteLinearlyPrecedesRead_IsTrueThroughANonConditionalWrapper()
    {
        var method = Method("using (x) { write(); } read();");

        Assert.True(WriteLinearlyPrecedesRead(Statement(method, "write();"), Statement(method, "read();")));
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("if (c) { write(); } read();")]
    [InlineData("while (c) { write(); } read();")]
    [InlineData("try { } catch { write(); } read();")]
    [InlineData("try { } finally { write(); } read();")]
    public void WriteLinearlyPrecedesRead_IsFalseWhenTheWriteIsConditional(string body)
    {
        var method = Method(body);

        Assert.False(WriteLinearlyPrecedesRead(Statement(method, "write();"), Statement(method, "read();")));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void WriteLinearlyPrecedesRead_IsFalseForStatementsOutsideAnyBlock()
    {
        var root = CSharpSyntaxTree.ParseText("write(); read();", cancellationToken: TestContext.Current.CancellationToken).GetRoot(TestContext.Current.CancellationToken);

        Assert.False(WriteLinearlyPrecedesRead(Statement(root, "write();"), Statement(root, "read();")));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void WriteBlockChildPrecedesReadBlockChild_ComparesTheTopLevelChildren()
    {
        var method = Method("{ write(); } read(); other();");
        var block = method.Body!;
        var write = Statement(method, "write();");
        var read = Statement(method, "read();");
        var outside = SyntaxFactory.ParseStatement("outside();");

        Assert.True(WriteBlockChildPrecedesReadBlockChild(block, write, read));
        Assert.False(WriteBlockChildPrecedesReadBlockChild(block, read, write));
        Assert.False(WriteBlockChildPrecedesReadBlockChild(block, write, write));
        Assert.False(WriteBlockChildPrecedesReadBlockChild(block, outside, read));
        Assert.False(WriteBlockChildPrecedesReadBlockChild(block, write, outside));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void IsConditionalConstruct_AcceptsEachConstructKind()
    {
        var method = Method("""
            if (c) { } else { }
            switch (v) { case 1: break; }
            do { } while (c);
            while (c) { }
            for (;;) { }
            foreach (var i in xs) { }
            try { } catch { } finally { }
            using (x) { }
            """);
        var kinds = method.DescendantNodes().Where(IsConditionalConstruct).Select(static n => n.GetType().Name).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(
        [
            nameof(CatchClauseSyntax),
            nameof(DoStatementSyntax),
            nameof(ElseClauseSyntax),
            nameof(FinallyClauseSyntax),
            nameof(ForEachStatementSyntax),
            nameof(ForStatementSyntax),
            nameof(IfStatementSyntax),
            nameof(SwitchSectionSyntax),
            nameof(SwitchStatementSyntax),
            nameof(WhileStatementSyntax)
        ],
            kinds);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void BuildAssignmentPositions_IndexesSimpleAssignmentsInSourceOrder()
    {
        var method = Method("""
            p = 1;
            q = 2;
            p = 3;
            this.r = 4;
            """);

        var positions = BuildAssignmentPositions(method);

        Assert.Equal("p|q", string.Join('|', positions.Keys.Order(StringComparer.Ordinal)));
        Assert.Equal(2, positions["p"].Count);
        Assert.True(positions["p"][0] < positions["p"][1]);
        _ = Assert.Single(positions["q"]);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("p = 1; write(); p = 2; read();", "Ident:p", "Ident:p", true)]
    [InlineData("write(); read(); p = 2;", "Ident:p", "Ident:p", false)]
    [InlineData("p = 1; write(); read();", "Ident:p", "Ident:p", false)]
    [InlineData("write(); other = 2; read();", "Ident:p", "Ident:p", false)]
    [InlineData("write(); p = 2; read();", "Expr:a", "Expr:b", false)]
    [InlineData("write(); p = 2; read();", "Expr:a", "Ident:p", true)]
    public void IsIdentifierReassignedBetween_FindsAnAssignmentBetweenWriteAndRead(
        string body,
        string writeKey,
        string readKey,
        bool expected)
    {
        var method = Method(body);
        var write = new WriteOp(Statement(method, "write();"), null, writeKey, null, IoKind.Text, false);
        var read = new ReadOp(Statement(method, "read();"), readKey, IoKind.Text, false, false);

        Assert.Equal(expected, IsIdentifierReassignedBetween(BuildAssignmentPositions(method), write, read));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void IsIdentifierReassignedBetween_IgnoresAnAssignmentAtTheWritePosition()
    {
        var method = Method("p = Save(); read();");
        var assignment = method.DescendantNodes().OfType<AssignmentExpressionSyntax>().Single();
        var write = new WriteOp(assignment, null, "Ident:p", null, IoKind.Text, false);
        var read = new ReadOp(Statement(method, "read();"), "Ident:p", IoKind.Text, false, false);

        Assert.False(IsIdentifierReassignedBetween(BuildAssignmentPositions(method), write, read));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void WriteOp_ComparesByEveryField()
    {
        var node = Expr("File.Create(a)");
        var source = Expr("data");
        var baseline = new WriteOp(node, node, "Ident:a", source, IoKind.Text, false);
        var same = new WriteOp(node, node, "Ident:a", source, IoKind.Text, false);

        Assert.True(baseline.Equals(same));
        Assert.True(baseline == same);
        Assert.False(baseline != same);
        Assert.True(baseline.Equals((object)same));
        Assert.Equal(baseline.GetHashCode(), same.GetHashCode());
        Assert.False(baseline.Equals("text"));
        Assert.False(baseline.Equals(new WriteOp(Expr("File.Create(a)"), node, "Ident:a", source, IoKind.Text, false)));
        Assert.False(baseline.Equals(new WriteOp(node, Expr("x"), "Ident:a", source, IoKind.Text, false)));
        Assert.False(baseline.Equals(new WriteOp(node, node, "Ident:b", source, IoKind.Text, false)));
        Assert.False(baseline.Equals(new WriteOp(node, node, "Ident:a", Expr("data"), IoKind.Text, false)));
        Assert.False(baseline.Equals(new WriteOp(node, node, "Ident:a", source, IoKind.Bytes, false)));
        Assert.False(baseline.Equals(new WriteOp(node, node, "Ident:a", source, IoKind.Text, true)));
        Assert.False(baseline.Equals(baseline.WithMergedSource(Expr("more")).WithMergedSource(Expr("again"))));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void WriteOp_HashCodeHandlesMissingOptionalNodes()
    {
        var node = Expr("File.Create(a)");
        var bare = new WriteOp(node, null, "Ident:a", null, IoKind.Text, true);

        Assert.Equal(bare.GetHashCode(), new WriteOp(node, null, "Ident:a", null, IoKind.Text, true).GetHashCode());
        Assert.NotEqual(bare.GetHashCode(), new WriteOp(node, null, "Ident:a", null, IoKind.Text, false).GetHashCode());
        Assert.Equal("WriteOp(Ident:a, Text)", bare.ToString());
    }

    [Fact]
    [Trait("Category", "CI")]
    public void WithMergedSource_KeepsTheFirstSourceThenMarksMultipleWrites()
    {
        var node = Expr("File.Create(a)");
        var first = Expr("one");
        var write = new WriteOp(node, node, "Ident:a", null, IoKind.Text, false);

        var withSource = write.WithMergedSource(first);
        var merged = withSource.WithMergedSource(Expr("two"));
        var mergedAgain = merged.WithMergedSource(Expr("three"));

        Assert.Same(first, withSource.SourceExpr);
        Assert.False(withSource.HasMultipleWrites);
        Assert.Null(merged.SourceExpr);
        Assert.True(merged.HasMultipleWrites);
        Assert.Null(mergedAgain.SourceExpr);
        Assert.True(mergedAgain.HasMultipleWrites);
        Assert.NotEqual(withSource.GetHashCode(), merged.GetHashCode());
    }

    [Fact]
    [Trait("Category", "CI")]
    public void ReadOp_ComparesByEveryField()
    {
        var node = Expr("File.ReadAllText(a)");
        var baseline = new ReadOp(node, "Ident:a", IoKind.Text, false, false);
        var same = new ReadOp(node, "Ident:a", IoKind.Text, false, false);

        Assert.True(baseline.Equals(same));
        Assert.True(baseline == same);
        Assert.False(baseline != same);
        Assert.True(baseline.Equals((object)same));
        Assert.Equal(baseline.GetHashCode(), same.GetHashCode());
        Assert.False(baseline.Equals("text"));
        Assert.False(baseline.Equals(new ReadOp(Expr("File.ReadAllText(a)"), "Ident:a", IoKind.Text, false, false)));
        Assert.False(baseline.Equals(new ReadOp(node, "Ident:b", IoKind.Text, false, false)));
        Assert.False(baseline.Equals(new ReadOp(node, "Ident:a", IoKind.Bytes, false, false)));
        Assert.False(baseline.Equals(new ReadOp(node, "Ident:a", IoKind.Text, true, false)));
        Assert.False(baseline.Equals(new ReadOp(node, "Ident:a", IoKind.Text, false, true)));
        Assert.NotEqual(baseline.GetHashCode(), new ReadOp(node, "Ident:a", IoKind.Text, true, false).GetHashCode());
        Assert.NotEqual(baseline.GetHashCode(), new ReadOp(node, "Ident:a", IoKind.Text, false, true).GetHashCode());
        Assert.Equal("ReadOp(Ident:a, Text)", baseline.ToString());
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Reliability;

/// <summary>
///     Central catalog of recognized System.IO write/read patterns for E128064
///     (disk write-then-read round-trip). A single source of truth for Tiers A–D.
/// </summary>
internal static class DiskIoCatalog
{
    private static readonly HashSet<string> WriterWriteMethods = new(StringComparer.Ordinal)
    {
        "Write",
        "WriteAsync",
        "WriteLine",
        "WriteLineAsync",
        "Flush",
        "FlushAsync"
    };

    private static readonly HashSet<string> WriteFileModes = new(StringComparer.Ordinal)
    {
        "Create",
        "CreateNew",
        "Append"
    };

    // The one source for File.* method membership. Every predicate below derives from this map.
    private static readonly Dictionary<string, IoKind> FileMethodKinds = new(StringComparer.Ordinal)
    {
        ["WriteAllText"] = IoKind.Text,
        ["WriteAllTextAsync"] = IoKind.Text,
        ["AppendAllText"] = IoKind.Text,
        ["AppendAllTextAsync"] = IoKind.Text,
        ["ReadAllText"] = IoKind.Text,
        ["ReadAllTextAsync"] = IoKind.Text,
        ["WriteAllBytes"] = IoKind.Bytes,
        ["WriteAllBytesAsync"] = IoKind.Bytes,
        ["ReadAllBytes"] = IoKind.Bytes,
        ["ReadAllBytesAsync"] = IoKind.Bytes,
        ["WriteAllLines"] = IoKind.Lines,
        ["WriteAllLinesAsync"] = IoKind.Lines,
        ["AppendAllLines"] = IoKind.Lines,
        ["AppendAllLinesAsync"] = IoKind.Lines,
        ["ReadAllLines"] = IoKind.Lines,
        ["ReadAllLinesAsync"] = IoKind.Lines,
        ["Create"] = IoKind.Stream,
        ["OpenWrite"] = IoKind.Stream,
        ["OpenRead"] = IoKind.Stream,
        ["CreateText"] = IoKind.Writer,
        ["AppendText"] = IoKind.Writer,
        ["OpenText"] = IoKind.Writer
    };

    internal static IoKind FileMethodKind(string methodName)
    {
        return FileMethodKinds.TryGetValue(methodName, out var kind) ? kind : IoKind.Unknown;
    }

    internal static bool IsFileWriteValueMethod(string name)
    {
        return IsValueMethod(name) && !name.StartsWith("Read", StringComparison.Ordinal);
    }

    internal static bool IsFileReadValueMethod(string name)
    {
        return IsValueMethod(name) && name.StartsWith("Read", StringComparison.Ordinal);
    }

    internal static bool IsFileWriteFactory(string name)
    {
        return IsFactory(name)
               && !name.StartsWith("OpenRead", StringComparison.Ordinal)
               && !name.StartsWith("OpenText", StringComparison.Ordinal);
    }

    internal static bool IsFileReadFactory(string name)
    {
        return IsFactory(name)
               && (name.StartsWith("OpenRead", StringComparison.Ordinal)
                   || name.StartsWith("OpenText", StringComparison.Ordinal));
    }

    private static bool IsValueMethod(string name)
    {
        return FileMethodKinds.TryGetValue(name, out var kind)
               && kind is IoKind.Text or IoKind.Bytes or IoKind.Lines;
    }

    private static bool IsFactory(string name)
    {
        return FileMethodKinds.TryGetValue(name, out var kind)
               && kind is IoKind.Stream or IoKind.Writer;
    }

    internal static bool IsAsyncName(string name)
    {
        return name.EndsWith("Async", StringComparison.Ordinal);
    }

    internal static bool IsWriterWriteMethod(string name)
    {
        return WriterWriteMethods.Contains(name);
    }

    internal static bool IsOpaqueWriteMethodName(string name)
    {
        return name.Contains("ToDisk", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("ToFile", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("SaveTo", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("WriteTo", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("DownloadTo", StringComparison.OrdinalIgnoreCase);
    }

    internal static string KindDescription(IoKind kind)
    {
        return kind switch
        {
            IoKind.Text => "text",
            IoKind.Bytes => "bytes",
            IoKind.Lines => "lines",
            IoKind.Stream => "stream",
            IoKind.Writer => "writer",
            IoKind.Reader => "reader",
            IoKind.Binary => "binary",
            IoKind.Unknown => "value",
            _ => "value"
        };
    }

    internal static string? ExtractCtorTypeName(TypeSyntax type)
    {
        return type switch
        {
            IdentifierNameSyntax id => id.Identifier.ValueText,
            QualifiedNameSyntax qn => qn.Right.Identifier.ValueText,
            GenericNameSyntax gn => gn.Identifier.ValueText,
            _ => null
        };
    }

    internal static bool IsReceiverType(ExpressionSyntax receiver, string expectedName)
    {
        return receiver is IdentifierNameSyntax id
               && string.Equals(id.Identifier.ValueText, expectedName, StringComparison.Ordinal);
    }

    internal static bool IsFileStreamWriteIntent(ExpressionSyntax? mode, ExpressionSyntax? access)
    {
        if (access is MemberAccessExpressionSyntax accessMa)
        {
            var name = accessMa.Name.Identifier.ValueText;
            return name switch
            {
                "Write" => true,
                "Read" => false,
                "ReadWrite" => true,
                _ => IsWriteFileMode(mode)
            };
        }

        return IsWriteFileMode(mode);
    }

    internal static bool IsWriteFileMode(ExpressionSyntax? mode)
    {
        return mode is MemberAccessExpressionSyntax modeMa
               && WriteFileModes.Contains(modeMa.Name.Identifier.ValueText);
    }

    internal static ExpressionSyntax? ExtractPathFromStreamArg(ExpressionSyntax arg)
    {
        return arg is InvocationExpressionSyntax inv
               && inv.Expression is MemberAccessExpressionSyntax ma
               && ma.Expression is IdentifierNameSyntax fileId
               && string.Equals(fileId.Identifier.ValueText, "File", StringComparison.Ordinal)
               && inv.ArgumentList.Arguments.Count >= 1
            ? inv.ArgumentList.Arguments[0].Expression
            : null;
    }

    internal static SyntaxNode GetAwaitedNodeOrSelf(InvocationExpressionSyntax invocation)
    {
        return invocation.Parent is AwaitExpressionSyntax awaitExpr ? awaitExpr : invocation;
    }

    internal static bool IsInsideAwait(InvocationExpressionSyntax invocation)
    {
        return invocation.Parent is AwaitExpressionSyntax;
    }

    internal static string Normalize(ExpressionSyntax expr)
    {
        return expr switch
        {
            IdentifierNameSyntax id => "Ident:" + id.Identifier.ValueText,
            MemberAccessExpressionSyntax ma when ma.Expression is IdentifierNameSyntax recv
                => "Member:" + recv.Identifier.ValueText + "." + ma.Name.Identifier.ValueText,
            MemberAccessExpressionSyntax ma when TryExtractChainRoot(ma, out var root)
                => "OpaqueWriteRoot:" + root,
            _ => "Expr:" + expr.ToFullString()
        };
    }

    internal static bool TryExtractChainRoot(MemberAccessExpressionSyntax ma, out string rootIdent)
    {
        var current = ma.Expression;
        while (current is MemberAccessExpressionSyntax inner)
        {
            current = inner.Expression;
        }

        if (current is IdentifierNameSyntax id)
        {
            rootIdent = id.Identifier.ValueText;
            return true;
        }

        rootIdent = string.Empty;
        return false;
    }

    internal static string NormalizeFileInfoInstance(ExpressionSyntax receiver)
    {
        return receiver is IdentifierNameSyntax id
            ? "FileInfo:" + id.Identifier.ValueText
            : "Expr:" + receiver.ToFullString();
    }

    internal static IEnumerable<string> CompatibleKeys(string key)
    {
        yield return key;
        if (key.StartsWith("Member:", StringComparison.Ordinal) && key.EndsWith(".FullName", StringComparison.Ordinal))
        {
            var inner = key.Substring("Member:".Length, key.Length - "Member:".Length - ".FullName".Length);
            yield return "FileInfo:" + inner;
        }
        else if (key.StartsWith("FileInfo:", StringComparison.Ordinal))
        {
            var inner = key.Substring("FileInfo:".Length);
            yield return "Member:" + inner + ".FullName";
        }
    }

    internal static bool KeysOverlap(string a, string b)
    {
        if (string.Equals(a, b, StringComparison.Ordinal))
        {
            return true;
        }

        foreach (var expanded in CompatibleKeys(a))
        {
            if (string.Equals(expanded, b, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return OpaqueWriteResultOverlap(a, b) || OpaqueWriteResultOverlap(b, a);
    }

    internal static bool OpaqueWriteResultOverlap(string writeKey, string readKey)
    {
        if (!writeKey.StartsWith("OpaqueWriteResult:", StringComparison.Ordinal))
        {
            return false;
        }

        var varName = writeKey.Substring("OpaqueWriteResult:".Length);

        return readKey.StartsWith("OpaqueWriteRoot:", StringComparison.Ordinal)
            ? string.Equals(varName, readKey.Substring("OpaqueWriteRoot:".Length), StringComparison.Ordinal)
            : readKey.StartsWith("Member:", StringComparison.Ordinal)
              && readKey.Substring("Member:".Length).StartsWith(varName + ".", StringComparison.Ordinal);
    }

    internal static string? ExtractIdentifier(string key)
    {
        if (key.StartsWith("Ident:", StringComparison.Ordinal))
        {
            return key.Substring("Ident:".Length);
        }

        if (key.StartsWith("Member:", StringComparison.Ordinal))
        {
            var dot = key.IndexOf('.', "Member:".Length);
            return dot > 0 ? key.Substring("Member:".Length, dot - "Member:".Length) : null;
        }

        if (key.StartsWith("FileInfo:", StringComparison.Ordinal))
        {
            return key.Substring("FileInfo:".Length);
        }

        if (key.StartsWith("OpaqueWriteResult:", StringComparison.Ordinal))
        {
            return key.Substring("OpaqueWriteResult:".Length);
        }

        return key.StartsWith("OpaqueWriteRoot:", StringComparison.Ordinal)
            ? key.Substring("OpaqueWriteRoot:".Length)
            : null;
    }

    internal static int FindFactoryIndex(List<WriteOp> writes, ExpressionSyntax candidate)
    {
        for (var i = 0; i < writes.Count; i++)
        {
            if (writes[i].FactoryNode == candidate)
            {
                return i;
            }
        }

        return -1;
    }

    internal static void TrackWriterVariable(
        VariableDeclarationSyntax decl,
        List<WriteOp> writes,
        Dictionary<string, int> writerVarToFactory)
    {
        foreach (var v in decl.Variables)
        {
            if (v.Initializer?.Value is null)
            {
                continue;
            }

            var factoryIndex = FindFactoryIndex(writes, v.Initializer.Value);
            if (factoryIndex >= 0)
            {
                writerVarToFactory[v.Identifier.ValueText] = factoryIndex;
            }
        }
    }

    internal static void TryTrackOpaqueWriteResult(
        VariableDeclarationSyntax decl,
        List<WriteOp> writes)
    {
        foreach (var v in decl.Variables)
        {
            var initializer = v.Initializer?.Value;
            if (initializer is null)
            {
                continue;
            }

            var invocation = initializer switch
            {
                AwaitExpressionSyntax { Expression: InvocationExpressionSyntax inv } => inv,
                InvocationExpressionSyntax inv => inv,
                _ => null
            };

            if (invocation?.Expression is not MemberAccessExpressionSyntax ma)
            {
                continue;
            }

            if (!IsOpaqueWriteMethodName(ma.Name.Identifier.ValueText))
            {
                continue;
            }

            writes.Add(new WriteOp(
                invocation,
                null,
                "OpaqueWriteResult:" + v.Identifier.ValueText,
                null,
                IoKind.Unknown,
                false));
        }
    }

    internal static bool WriteLinearlyPrecedesRead(StatementSyntax writeStmt, StatementSyntax readStmt)
    {
        if (writeStmt.SpanStart >= readStmt.SpanStart)
        {
            return false;
        }

        SyntaxNode? cursor = writeStmt;
        while (cursor is not null)
        {
            var parent = cursor.Parent;
            if (parent is null)
            {
                return false;
            }

            if (parent is BlockSyntax block)
            {
                if (block.Contains(readStmt))
                {
                    return WriteBlockChildPrecedesReadBlockChild(block, writeStmt, readStmt);
                }

                cursor = block;
                continue;
            }

            if (IsConditionalConstruct(parent))
            {
                return false;
            }

            cursor = parent;
        }

        return false;
    }

    internal static bool WriteBlockChildPrecedesReadBlockChild(BlockSyntax block, StatementSyntax writeStmt, StatementSyntax readStmt)
    {
        StatementSyntax? wChild = null;
        StatementSyntax? rChild = null;
        foreach (var s in block.Statements)
        {
            if (s.Span.Contains(writeStmt.Span))
            {
                wChild = s;
            }

            if (s.Span.Contains(readStmt.Span))
            {
                rChild = s;
            }
        }

        return wChild is not null && rChild is not null && wChild.SpanStart < rChild.SpanStart;
    }

    internal static bool IsConditionalConstruct(SyntaxNode node)
    {
        return node is IfStatementSyntax or ElseClauseSyntax or SwitchSectionSyntax
            or SwitchStatementSyntax or DoStatementSyntax or WhileStatementSyntax
            or ForStatementSyntax or ForEachStatementSyntax or CatchClauseSyntax
            or FinallyClauseSyntax;
    }

    /// <summary>
    ///     Indexes every simple assignment in the body by the assigned identifier. The positions are
    ///     in source order because <c lang="csharp">DescendantNodes</c> walks in document order.
    /// </summary>
    internal static Dictionary<string, List<int>> BuildAssignmentPositions(SyntaxNode body)
    {
        var result = new Dictionary<string, List<int>>(StringComparer.Ordinal);

        foreach (var node in body.DescendantNodes())
        {
            if (node is AssignmentExpressionSyntax { Left: IdentifierNameSyntax left } assignment)
            {
                if (!result.TryGetValue(left.Identifier.ValueText, out var positions))
                {
                    positions = [];
                    result[left.Identifier.ValueText] = positions;
                }

                positions.Add(assignment.SpanStart);
            }
        }

        return result;
    }

    internal static bool IsIdentifierReassignedBetween(
        IReadOnlyDictionary<string, List<int>> assignmentsByIdentifier,
        WriteOp write,
        ReadOp read)
    {
        var ident = ExtractIdentifier(write.PathKey) ?? ExtractIdentifier(read.PathKey);
        if (ident is null || !assignmentsByIdentifier.TryGetValue(ident, out var positions))
        {
            return false;
        }

        var writePos = write.ReportNode.SpanStart;
        var readPos = read.ReportNode.SpanStart;

        // First assignment strictly after the write position.
        var index = positions.BinarySearch(writePos);
        index = index >= 0 ? index + 1 : ~index;

        return index < positions.Count && positions[index] < readPos;
    }

    internal enum IoKind
    {
        Text = 0,
        Bytes = 1,
        Lines = 2,
        Stream = 3,
        Writer = 4,
        Reader = 5,
        Binary = 6,
        Unknown = 7
    }

    internal readonly struct WriteOp : IEquatable<WriteOp>
    {
        private const int HashPrime = 31;

        internal WriteOp(
            SyntaxNode reportNode,
            ExpressionSyntax? factoryNode,
            string pathKey,
            ExpressionSyntax? sourceExpr,
            IoKind kind,
            bool isAsync)
        {
            ReportNode = reportNode;
            FactoryNode = factoryNode;
            PathKey = pathKey;
            SourceExpr = sourceExpr;
            Kind = kind;
            IsAsync = isAsync;
            HasMultipleWrites = false;
        }

        private WriteOp(WriteOp source, ExpressionSyntax? sourceExpr, bool hasMultipleWrites)
        {
            ReportNode = source.ReportNode;
            FactoryNode = source.FactoryNode;
            PathKey = source.PathKey;
            SourceExpr = sourceExpr;
            Kind = source.Kind;
            IsAsync = source.IsAsync;
            HasMultipleWrites = hasMultipleWrites;
        }

        internal SyntaxNode ReportNode { get; }
        internal ExpressionSyntax? FactoryNode { get; }
        internal string PathKey { get; }
        internal ExpressionSyntax? SourceExpr { get; }
        internal IoKind Kind { get; }
        internal bool IsAsync { get; }
        internal bool HasMultipleWrites { get; }

        internal WriteOp WithMergedSource(ExpressionSyntax src)
        {
            if (SourceExpr is null && !HasMultipleWrites)
            {
                return new WriteOp(this, src, false);
            }

            return new WriteOp(this, null, true);
        }

        public bool Equals(WriteOp other)
        {
            return ReportNode == other.ReportNode
                   && FactoryNode == other.FactoryNode
                   && string.Equals(PathKey, other.PathKey, StringComparison.Ordinal)
                   && SourceExpr == other.SourceExpr
                   && Kind == other.Kind
                   && IsAsync == other.IsAsync
                   && HasMultipleWrites == other.HasMultipleWrites;
        }

        public override bool Equals([NotNullWhen(true)] object? obj)
        {
            return obj is WriteOp other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = ReportNode.GetHashCode();
                hash = (hash * HashPrime) + (FactoryNode?.GetHashCode() ?? 0);
                hash = (hash * HashPrime) + StringComparer.Ordinal.GetHashCode(PathKey);
                hash = (hash * HashPrime) + (SourceExpr?.GetHashCode() ?? 0);
                hash = (hash * HashPrime) + (int)Kind;
                hash = (hash * HashPrime) + (IsAsync ? 1 : 0);
                return (hash * HashPrime) + (HasMultipleWrites ? 1 : 0);
            }
        }

        public override string ToString()
        {
            var kind = Kind.ToString();
            return $"WriteOp({PathKey}, {kind})";
        }

        public static bool operator ==(WriteOp left, WriteOp right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(WriteOp left, WriteOp right)
        {
            return !left.Equals(right);
        }
    }

    internal readonly struct ReadOp : IEquatable<ReadOp>
    {
        private const int HashPrime = 31;

        internal ReadOp(
            SyntaxNode reportNode,
            string pathKey,
            IoKind kind,
            bool isAsync,
            bool isAwaited)
        {
            ReportNode = reportNode;
            PathKey = pathKey;
            Kind = kind;
            IsAsync = isAsync;
            IsAwaited = isAwaited;
        }

        internal SyntaxNode ReportNode { get; }
        internal string PathKey { get; }
        internal IoKind Kind { get; }
        internal bool IsAsync { get; }
        internal bool IsAwaited { get; }

        public bool Equals(ReadOp other)
        {
            return ReportNode == other.ReportNode
                   && string.Equals(PathKey, other.PathKey, StringComparison.Ordinal)
                   && Kind == other.Kind
                   && IsAsync == other.IsAsync
                   && IsAwaited == other.IsAwaited;
        }

        public override bool Equals([NotNullWhen(true)] object? obj)
        {
            return obj is ReadOp other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = ReportNode.GetHashCode();
                hash = (hash * HashPrime) + StringComparer.Ordinal.GetHashCode(PathKey);
                hash = (hash * HashPrime) + (int)Kind;
                hash = (hash * HashPrime) + (IsAsync ? 1 : 0);
                return (hash * HashPrime) + (IsAwaited ? 1 : 0);
            }
        }

        public override string ToString()
        {
            var kind = Kind.ToString();
            return $"ReadOp({PathKey}, {kind})";
        }

        public static bool operator ==(ReadOp left, ReadOp right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ReadOp left, ReadOp right)
        {
            return !left.Equals(right);
        }
    }
}

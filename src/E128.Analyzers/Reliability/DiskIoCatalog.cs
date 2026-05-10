using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Reliability;

/// <summary>
///     Central catalog of recognized System.IO write/read patterns for E128064
///     (disk write-then-read round-trip). A single source of truth for Tiers A–D.
/// </summary>
internal static class DiskIoCatalog
{
    private static readonly HashSet<string> FileWriteValueMethods = new(StringComparer.Ordinal)
    {
        "WriteAllText",
        "WriteAllTextAsync",
        "WriteAllBytes",
        "WriteAllBytesAsync",
        "WriteAllLines",
        "WriteAllLinesAsync",
        "AppendAllText",
        "AppendAllTextAsync",
        "AppendAllLines",
        "AppendAllLinesAsync"
    };

    private static readonly HashSet<string> FileReadValueMethods = new(StringComparer.Ordinal)
    {
        "ReadAllText",
        "ReadAllTextAsync",
        "ReadAllBytes",
        "ReadAllBytesAsync",
        "ReadAllLines",
        "ReadAllLinesAsync"
    };

    // File.* factories that produce a Stream/StreamWriter targeting a path for WRITE.
    private static readonly HashSet<string> FileWriteFactories = new(StringComparer.Ordinal)
    {
        "Create",
        "CreateText",
        "OpenWrite",
        "AppendText"
    };

    // File.* factories that produce a Stream/StreamReader targeting a path for READ.
    private static readonly HashSet<string> FileReadFactories = new(StringComparer.Ordinal)
    {
        "OpenRead",
        "OpenText"
    };

    internal static IoKind FileMethodKind(string methodName)
    {
        return methodName switch
        {
            "WriteAllText" or "WriteAllTextAsync" or "AppendAllText" or "AppendAllTextAsync"
                or "ReadAllText" or "ReadAllTextAsync" => IoKind.Text,
            "WriteAllBytes" or "WriteAllBytesAsync" or "ReadAllBytes" or "ReadAllBytesAsync" => IoKind.Bytes,
            "WriteAllLines" or "WriteAllLinesAsync" or "AppendAllLines" or "AppendAllLinesAsync"
                or "ReadAllLines" or "ReadAllLinesAsync" => IoKind.Lines,
            "Create" or "OpenWrite" or "OpenRead" => IoKind.Stream,
            "CreateText" or "AppendText" or "OpenText" => IoKind.Writer,
            _ => IoKind.Unknown
        };
    }

    internal static bool IsFileWriteValueMethod(string name)
    {
        return FileWriteValueMethods.Contains(name);
    }

    internal static bool IsFileReadValueMethod(string name)
    {
        return FileReadValueMethods.Contains(name);
    }

    internal static bool IsFileWriteFactory(string name)
    {
        return FileWriteFactories.Contains(name);
    }

    internal static bool IsFileReadFactory(string name)
    {
        return FileReadFactories.Contains(name);
    }

    internal static bool IsAsyncName(string name)
    {
        return name.EndsWith("Async", StringComparison.Ordinal);
    }

    internal static bool IsWriterWriteMethod(string name)
    {
        return name is "Write" or "WriteAsync" or "WriteLine" or "WriteLineAsync" or "Flush" or "FlushAsync";
    }

    internal static bool IsOpaqueWriteMethodName(string name)
    {
        return name.Contains("ToDisk", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("ToFile", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("SaveTo", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("WriteTo", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("DownloadTo", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsReaderReadMethod(string name)
    {
        return name is "Read" or "ReadAsync" or "ReadToEnd" or "ReadToEndAsync"
            or "ReadLine" or "ReadLineAsync" or "ReadInt32" or "ReadInt64"
            or "ReadByte" or "ReadBytes" or "ReadString" or "ReadBoolean"
            or "ReadSingle" or "ReadDouble" or "ReadDecimal" or "ReadChar" or "ReadChars";
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
               && modeMa.Name.Identifier.ValueText is "Create" or "CreateNew" or "Append";
    }

    internal static ExpressionSyntax? ExtractPathFromStreamArg(ExpressionSyntax arg)
    {
        return arg is InvocationExpressionSyntax inv
               && inv.Expression is MemberAccessExpressionSyntax ma
               && ma.Expression is IdentifierNameSyntax { Identifier.ValueText: "File" }
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

        return key.StartsWith("FileInfo:", StringComparison.Ordinal)
            ? key.Substring("FileInfo:".Length)
            : key.StartsWith("OpaqueWriteResult:", StringComparison.Ordinal)
                ? key.Substring("OpaqueWriteResult:".Length)
                : key.StartsWith("OpaqueWriteRoot:", StringComparison.Ordinal)
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
        Dictionary<string, int> writerVarToFactory,
        SyntaxNode? enclosingUsing)
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
                if (enclosingUsing is not null)
                {
                    writes[factoryIndex] = writes[factoryIndex].WithDisposalBoundary(enclosingUsing.Span.End);
                }
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
                false,
                false,
                invocation.SpanStart));
        }
    }

    internal static bool WriteLinearlyPrecedesRead(StatementSyntax writeStmt, StatementSyntax readStmt)
    {
        if (writeStmt.SpanStart >= readStmt.SpanStart)
        {
            return false;
        }

        SyntaxNode? cursor = writeStmt;
        while (cursor != null)
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

    internal static bool IsIdentifierReassignedBetween(SyntaxNode body, WriteOp write, ReadOp read)
    {
        var ident = ExtractIdentifier(write.PathKey) ?? ExtractIdentifier(read.PathKey);
        if (ident is null)
        {
            return false;
        }

        var writePos = write.ReportNode.SpanStart;
        var readPos = read.ReportNode.SpanStart;

        foreach (var assign in body.DescendantNodes())
        {
            if (assign is AssignmentExpressionSyntax assignment
                && assignment.SpanStart > writePos
                && assignment.SpanStart < readPos
                && assignment.Left is IdentifierNameSyntax left
                && string.Equals(left.Identifier.ValueText, ident, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
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

    internal struct WriteOp
    {
        public WriteOp(
            SyntaxNode reportNode,
            ExpressionSyntax? factoryNode,
            string pathKey,
            ExpressionSyntax? sourceExpr,
            IoKind kind,
            bool isAsync,
            bool isStreamFactory,
            int disposalBoundary)
        {
            ReportNode = reportNode;
            FactoryNode = factoryNode;
            PathKey = pathKey;
            SourceExpr = sourceExpr;
            Kind = kind;
            IsAsync = isAsync;
            IsStreamFactory = isStreamFactory;
            DisposalBoundary = disposalBoundary;
            HasMultipleWrites = false;
        }

        public SyntaxNode ReportNode { get; }
        public ExpressionSyntax? FactoryNode { get; }
        public string PathKey { get; }
        public ExpressionSyntax? SourceExpr { get; set; }
        public IoKind Kind { get; }
        public bool IsAsync { get; }
        public bool IsStreamFactory { get; }
        public int DisposalBoundary { get; set; }
        public bool HasMultipleWrites { get; set; }

        public readonly WriteOp WithDisposalBoundary(int pos)
        {
            var copy = this;
            copy.DisposalBoundary = pos;
            return copy;
        }

        public readonly WriteOp WithMergedSource(ExpressionSyntax src)
        {
            var copy = this;
            if (copy.SourceExpr is null && !copy.HasMultipleWrites)
            {
                copy.SourceExpr = src;
            }
            else
            {
                copy.HasMultipleWrites = true;
                copy.SourceExpr = null;
            }

            return copy;
        }
    }

    internal readonly struct ReadOp
    {
        public ReadOp(
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

        public SyntaxNode ReportNode { get; }
        public string PathKey { get; }
        public IoKind Kind { get; }
        public bool IsAsync { get; }
        public bool IsAwaited { get; }
    }
}

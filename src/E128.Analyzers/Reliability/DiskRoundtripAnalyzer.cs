using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128064: Detects writing a value to disk and immediately reading it back in the same method.
///     Covers the full <c>System.IO.*</c> surface — static <c>File.*</c> value-level APIs, stream
///     factories (<c>File.Create</c>, <c>File.CreateText</c>, …), explicit constructors
///     (<c>new FileStream</c>, <c>new StreamWriter</c>, <c>new StreamReader</c>, <c>new BinaryWriter/Reader</c>),
///     and <c>FileInfo</c> instance methods (<c>Create</c>, <c>OpenWrite</c>, <c>CreateText</c>, <c>AppendText</c>,
///     <c>OpenRead</c>, <c>OpenText</c>) — both sync and async.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DiskRoundtripAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128064";

    internal const string PropWriteKind = "WriteKind";
    internal const string PropReadKind = "ReadKind";
    internal const string PropSourceExpression = "SourceExpression";
    internal const string PropIsAwaited = "IsAwaited";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Write-then-read round-trip via disk — use the in-memory value",
        "Disk round-trip: {0} write followed by {1} read on the same path — use the in-memory value directly instead of reading from disk",
        "Reliability",
        DiagnosticSeverity.Warning,
        true,
        "Writing a value to disk and immediately reading it back in the same method is wasted I/O and a reliability hazard (another process may change or lock the file between the write and the read). The in-memory value is the authoritative source; use it directly.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            AnalyzeFunctionBody,
            SyntaxKind.MethodDeclaration,
            SyntaxKind.LocalFunctionStatement,
            SyntaxKind.ConstructorDeclaration);
    }

    private static void AnalyzeFunctionBody(SyntaxNodeAnalysisContext context)
    {
        var body = context.Node switch
        {
            MethodDeclarationSyntax m => (SyntaxNode?)m.Body ?? m.ExpressionBody?.Expression,
            LocalFunctionStatementSyntax lf => (SyntaxNode?)lf.Body ?? lf.ExpressionBody?.Expression,
            ConstructorDeclarationSyntax ct => (SyntaxNode?)ct.Body ?? ct.ExpressionBody?.Expression,
            _ => null
        };
        if (body is null)
        {
            return;
        }

        var writes = new List<WriteOp>();
        var reads = new List<ReadOp>();
        var writerVarToFactory = new Dictionary<string, int>(StringComparer.Ordinal);

        CollectOps(context, body, writes, reads, writerVarToFactory);
        if (writes.Count == 0 || reads.Count == 0)
        {
            return;
        }

        Correlate(context, body, writes, reads);
    }

    private static void CollectOps(
        SyntaxNodeAnalysisContext context,
        SyntaxNode body,
        List<WriteOp> writes,
        List<ReadOp> reads,
        Dictionary<string, int> writerVarToFactory)
    {
        foreach (var node in body.DescendantNodes())
        {
            if (node is InvocationExpressionSyntax invocation)
            {
                TryClassifyInvocation(context, invocation, writes, reads, writerVarToFactory);
            }
            else if (node is ObjectCreationExpressionSyntax creation)
            {
                TryClassifyCtor(context, creation, writes, reads);
            }
            else if (node is LocalDeclarationStatementSyntax local)
            {
                TrackWriterVariable(local.Declaration, writes, writerVarToFactory, null);
            }
            else if (node is UsingStatementSyntax usingStmt && usingStmt.Declaration is { } decl)
            {
                TrackWriterVariable(decl, writes, writerVarToFactory, usingStmt);
            }
        }
    }

    // Tracks local variable decls whose initializer is a write-factory call, so that
    // subsequent `.Write(...)` invocations on that variable can be attributed to the factory.
    private static void TrackWriterVariable(
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

    private static int FindFactoryIndex(List<WriteOp> writes, ExpressionSyntax candidate)
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

    private static void TryClassifyInvocation(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        List<WriteOp> writes,
        List<ReadOp> reads,
        Dictionary<string, int> writerVarToFactory)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        var methodName = memberAccess.Name.Identifier.ValueText;
        var receiver = memberAccess.Expression;

        if (TryHandleFileStatic(context, invocation, receiver, methodName, writes, reads))
        {
            return;
        }

        if (TryHandleFileInfoInstance(context, invocation, receiver, methodName, writes, reads))
        {
            return;
        }

        TryHandleWriterWrite(invocation, receiver, methodName, writes, writerVarToFactory);
    }

    private static bool TryHandleFileStatic(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        ExpressionSyntax receiver,
        string methodName,
        List<WriteOp> writes,
        List<ReadOp> reads)
    {
        if (!IsReceiverType(receiver, "File"))
        {
            return false;
        }

        if (DiskIoCatalog.IsFileWriteValueMethod(methodName))
        {
            if (!ConfirmSystemIoInvocation(context, invocation))
            {
                return true;
            }

            AddFileValueWrite(invocation, methodName, writes);
            return true;
        }

        if (DiskIoCatalog.IsFileReadValueMethod(methodName))
        {
            if (!ConfirmSystemIoInvocation(context, invocation))
            {
                return true;
            }

            AddFileValueRead(invocation, methodName, reads);
            return true;
        }

        if (DiskIoCatalog.IsFileWriteFactory(methodName))
        {
            if (!ConfirmSystemIoInvocation(context, invocation))
            {
                return true;
            }

            AddFileWriteFactory(invocation, methodName, writes);
            return true;
        }

        if (DiskIoCatalog.IsFileReadFactory(methodName))
        {
            if (!ConfirmSystemIoInvocation(context, invocation))
            {
                return true;
            }

            AddFileReadFactory(invocation, methodName, reads);
            return true;
        }

        return false;
    }

    private static void AddFileValueWrite(InvocationExpressionSyntax invocation, string methodName, List<WriteOp> writes)
    {
        var args = invocation.ArgumentList.Arguments;
        if (args.Count < 2)
        {
            return;
        }

        writes.Add(new WriteOp(
            invocation,
            null,
            Normalize(args[0].Expression),
            args[1].Expression,
            DiskIoCatalog.FileMethodKind(methodName),
            DiskIoCatalog.IsAsyncName(methodName),
            false,
            invocation.SpanStart));
    }

    private static void AddFileValueRead(InvocationExpressionSyntax invocation, string methodName, List<ReadOp> reads)
    {
        var args = invocation.ArgumentList.Arguments;
        if (args.Count < 1)
        {
            return;
        }

        reads.Add(new ReadOp(
            GetAwaitedNodeOrSelf(invocation),
            Normalize(args[0].Expression),
            DiskIoCatalog.FileMethodKind(methodName),
            DiskIoCatalog.IsAsyncName(methodName),
            IsInsideAwait(invocation)));
    }

    private static void AddFileWriteFactory(InvocationExpressionSyntax invocation, string methodName, List<WriteOp> writes)
    {
        var args = invocation.ArgumentList.Arguments;
        if (args.Count < 1)
        {
            return;
        }

        writes.Add(new WriteOp(
            invocation,
            invocation,
            Normalize(args[0].Expression),
            null,
            DiskIoCatalog.FileMethodKind(methodName),
            false,
            true,
            int.MaxValue));
    }

    private static void AddFileReadFactory(InvocationExpressionSyntax invocation, string methodName, List<ReadOp> reads)
    {
        var args = invocation.ArgumentList.Arguments;
        if (args.Count < 1)
        {
            return;
        }

        reads.Add(new ReadOp(
            invocation,
            Normalize(args[0].Expression),
            DiskIoCatalog.FileMethodKind(methodName),
            false,
            false));
    }

    private static bool TryHandleFileInfoInstance(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        ExpressionSyntax receiver,
        string methodName,
        List<WriteOp> writes,
        List<ReadOp> reads)
    {
        if (!IsFileInfoReceiver(context, receiver))
        {
            return false;
        }

        if (DiskIoCatalog.IsFileWriteFactory(methodName))
        {
            writes.Add(new WriteOp(
                invocation,
                invocation,
                NormalizeFileInfoInstance(receiver),
                null,
                DiskIoCatalog.FileMethodKind(methodName),
                false,
                true,
                int.MaxValue));
            return true;
        }

        if (DiskIoCatalog.IsFileReadFactory(methodName))
        {
            reads.Add(new ReadOp(
                invocation,
                NormalizeFileInfoInstance(receiver),
                DiskIoCatalog.FileMethodKind(methodName),
                false,
                false));
            return true;
        }

        return false;
    }

    private static void TryHandleWriterWrite(
        InvocationExpressionSyntax invocation,
        ExpressionSyntax receiver,
        string methodName,
        List<WriteOp> writes,
        Dictionary<string, int> writerVarToFactory)
    {
        if (!DiskIoCatalog.IsWriterWriteMethod(methodName))
        {
            return;
        }

        if (receiver is not IdentifierNameSyntax writerId)
        {
            return;
        }

        if (!writerVarToFactory.TryGetValue(writerId.Identifier.ValueText, out var factoryIdx))
        {
            return;
        }

        var args = invocation.ArgumentList.Arguments;
        if (args.Count >= 1 && !methodName.StartsWith("Flush", StringComparison.Ordinal))
        {
            writes[factoryIdx] = writes[factoryIdx].WithMergedSource(args[0].Expression);
        }
    }

    private static void TryClassifyCtor(
        SyntaxNodeAnalysisContext context,
        ObjectCreationExpressionSyntax creation,
        List<WriteOp> writes,
        List<ReadOp> reads)
    {
        var typeName = ExtractCtorTypeName(creation.Type);
        if (typeName is null)
        {
            return;
        }

        var args = creation.ArgumentList?.Arguments;
        if (args is null || !args.Value.Any())
        {
            return;
        }

        switch (typeName)
        {
            case "StreamWriter":
                if (ConfirmSystemIoCtor(context, creation))
                {
                    AddStreamWriterCtor(creation, args.Value, writes);
                }

                break;

            case "StreamReader":
                if (ConfirmSystemIoCtor(context, creation))
                {
                    AddStreamReaderCtor(creation, args.Value, reads);
                }

                break;

            case "FileStream":
                if (ConfirmSystemIoCtor(context, creation))
                {
                    AddFileStreamCtor(creation, args.Value, writes, reads);
                }

                break;

            case "BinaryWriter":
                if (ConfirmSystemIoCtor(context, creation))
                {
                    AddBinaryWriterCtor(creation, args.Value, writes);
                }

                break;

            case "BinaryReader":
                if (ConfirmSystemIoCtor(context, creation))
                {
                    AddBinaryReaderCtor(creation, args.Value, reads);
                }

                break;

            default:
                // Not a System.IO stream ctor we recognize; ignore.
                return;
        }
    }

    private static void AddStreamWriterCtor(
        ObjectCreationExpressionSyntax creation,
        SeparatedSyntaxList<ArgumentSyntax> args,
        List<WriteOp> writes)
    {
        writes.Add(new WriteOp(
            creation,
            creation,
            Normalize(args[0].Expression),
            null,
            DiskIoCatalog.IoKind.Writer,
            false,
            true,
            int.MaxValue));
    }

    private static void AddStreamReaderCtor(
        ObjectCreationExpressionSyntax creation,
        SeparatedSyntaxList<ArgumentSyntax> args,
        List<ReadOp> reads)
    {
        reads.Add(new ReadOp(
            creation,
            Normalize(args[0].Expression),
            DiskIoCatalog.IoKind.Reader,
            false,
            false));
    }

    private static void AddFileStreamCtor(
        ObjectCreationExpressionSyntax creation,
        SeparatedSyntaxList<ArgumentSyntax> args,
        List<WriteOp> writes,
        List<ReadOp> reads)
    {
        var mode = args.Count >= 2 ? args[1].Expression : null;
        var access = args.Count >= 3 ? args[2].Expression : null;
        var isWrite = IsFileStreamWriteIntent(mode, access);
        if (isWrite)
        {
            writes.Add(new WriteOp(
                creation,
                creation,
                Normalize(args[0].Expression),
                null,
                DiskIoCatalog.IoKind.Stream,
                false,
                true,
                int.MaxValue));
        }
        else
        {
            reads.Add(new ReadOp(
                creation,
                Normalize(args[0].Expression),
                DiskIoCatalog.IoKind.Stream,
                false,
                false));
        }
    }

    private static void AddBinaryWriterCtor(
        ObjectCreationExpressionSyntax creation,
        SeparatedSyntaxList<ArgumentSyntax> args,
        List<WriteOp> writes)
    {
        var innerWritePath = ExtractPathFromStreamArg(args[0].Expression);
        if (innerWritePath is not null)
        {
            writes.Add(new WriteOp(
                creation,
                creation,
                Normalize(innerWritePath),
                null,
                DiskIoCatalog.IoKind.Binary,
                false,
                true,
                int.MaxValue));
        }
    }

    private static void AddBinaryReaderCtor(
        ObjectCreationExpressionSyntax creation,
        SeparatedSyntaxList<ArgumentSyntax> args,
        List<ReadOp> reads)
    {
        var innerReadPath = ExtractPathFromStreamArg(args[0].Expression);
        if (innerReadPath is not null)
        {
            reads.Add(new ReadOp(
                creation,
                Normalize(innerReadPath),
                DiskIoCatalog.IoKind.Binary,
                false,
                false));
        }
    }

    private static ExpressionSyntax? ExtractPathFromStreamArg(ExpressionSyntax arg)
    {
        return arg is InvocationExpressionSyntax inv
               && inv.Expression is MemberAccessExpressionSyntax ma
               && ma.Expression is IdentifierNameSyntax { Identifier.ValueText: "File" }
               && inv.ArgumentList.Arguments.Count >= 1
            ? inv.ArgumentList.Arguments[0].Expression
            : null;
    }

    private static bool IsFileStreamWriteIntent(ExpressionSyntax? mode, ExpressionSyntax? access)
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

    private static bool IsWriteFileMode(ExpressionSyntax? mode)
    {
        return mode is MemberAccessExpressionSyntax modeMa
               && modeMa.Name.Identifier.ValueText is "Create" or "CreateNew" or "Append";
    }

    private static string? ExtractCtorTypeName(TypeSyntax type)
    {
        return type switch
        {
            IdentifierNameSyntax id => id.Identifier.ValueText,
            QualifiedNameSyntax qn => qn.Right.Identifier.ValueText,
            GenericNameSyntax gn => gn.Identifier.ValueText,
            _ => null
        };
    }

    private static bool IsReceiverType(ExpressionSyntax receiver, string expectedName)
    {
        return receiver is IdentifierNameSyntax id
               && string.Equals(id.Identifier.ValueText, expectedName, StringComparison.Ordinal);
    }

    private static bool IsFileInfoReceiver(SyntaxNodeAnalysisContext context, ExpressionSyntax receiver)
    {
        if (receiver is not IdentifierNameSyntax id)
        {
            return false;
        }

        var typeInfo = context.SemanticModel.GetTypeInfo(id, context.CancellationToken);
        var symbol = typeInfo.Type;
        return symbol is not null
               && string.Equals(symbol.Name, "FileInfo", StringComparison.Ordinal)
               && string.Equals(symbol.ContainingNamespace?.ToDisplayString(), "System.IO", StringComparison.Ordinal);
    }

    private static bool ConfirmSystemIoInvocation(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation)
    {
        var symbol = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol;
        return symbol is IMethodSymbol method
               && string.Equals(method.ContainingType?.ContainingNamespace?.ToDisplayString(), "System.IO", StringComparison.Ordinal);
    }

    private static bool ConfirmSystemIoCtor(SyntaxNodeAnalysisContext context, ObjectCreationExpressionSyntax creation)
    {
        var symbol = context.SemanticModel.GetSymbolInfo(creation, context.CancellationToken).Symbol;
        return symbol is IMethodSymbol method
               && string.Equals(method.ContainingType?.ContainingNamespace?.ToDisplayString(), "System.IO", StringComparison.Ordinal);
    }

    private static string Normalize(ExpressionSyntax expr)
    {
        return expr switch
        {
            IdentifierNameSyntax id => "Ident:" + id.Identifier.ValueText,
            MemberAccessExpressionSyntax ma when ma.Expression is IdentifierNameSyntax recv
                => "Member:" + recv.Identifier.ValueText + "." + ma.Name.Identifier.ValueText,
            _ => "Expr:" + expr.ToFullString()
        };
    }

    private static string NormalizeFileInfoInstance(ExpressionSyntax receiver)
    {
        return receiver is IdentifierNameSyntax id
            ? "FileInfo:" + id.Identifier.ValueText
            : "Expr:" + receiver.ToFullString();
    }

    // A read's matching write may target the FileInfo instance while the read targets fi.FullName
    // (Tier D cross-key). This helper expands a path key into the set of compatible keys.
    private static IEnumerable<string> CompatibleKeys(string key)
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

    private static SyntaxNode GetAwaitedNodeOrSelf(InvocationExpressionSyntax invocation)
    {
        return invocation.Parent is AwaitExpressionSyntax awaitExpr ? awaitExpr : invocation;
    }

    private static bool IsInsideAwait(InvocationExpressionSyntax invocation)
    {
        return invocation.Parent is AwaitExpressionSyntax;
    }

    private static void Correlate(
        SyntaxNodeAnalysisContext context,
        SyntaxNode body,
        List<WriteOp> writes,
        List<ReadOp> reads)
    {
        foreach (var read in reads)
        {
            var readStmt = read.ReportNode.FirstAncestorOrSelf<StatementSyntax>();
            if (readStmt is null)
            {
                continue;
            }

            var match = FindMatchingWrite(body, writes, read, readStmt);
            if (match is null)
            {
                continue;
            }

            ReportRoundtrip(context, read, match.Value);
        }
    }

    private static WriteOp? FindMatchingWrite(
        SyntaxNode body,
        List<WriteOp> writes,
        ReadOp read,
        StatementSyntax readStmt)
    {
        for (var i = writes.Count - 1; i >= 0; i--)
        {
            var write = writes[i];
            if (!KeysOverlap(write.PathKey, read.PathKey))
            {
                continue;
            }

            var writeStmt = write.ReportNode.FirstAncestorOrSelf<StatementSyntax>();
            if (writeStmt is null)
            {
                continue;
            }

            if (!WriteLinearlyPrecedesRead(writeStmt, readStmt))
            {
                continue;
            }

            if (IsIdentifierReassignedBetween(body, write, read))
            {
                continue;
            }

            return write;
        }

        return null;
    }

    private static void ReportRoundtrip(SyntaxNodeAnalysisContext context, ReadOp read, WriteOp match)
    {
        var properties = ImmutableDictionary<string, string?>.Empty
            .Add(PropWriteKind, match.Kind.ToString())
            .Add(PropReadKind, read.Kind.ToString())
            .Add(PropSourceExpression, match.SourceExpr?.ToString())
            .Add(PropIsAwaited, read.IsAwaited ? "true" : "false");

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            read.ReportNode.GetLocation(),
            properties,
            DiskIoCatalog.KindDescription(match.Kind),
            DiskIoCatalog.KindDescription(read.Kind)));
    }

    private static bool KeysOverlap(string a, string b)
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

        return false;
    }

    private static bool WriteLinearlyPrecedesRead(StatementSyntax writeStmt, StatementSyntax readStmt)
    {
        if (writeStmt.SpanStart >= readStmt.SpanStart)
        {
            return false;
        }

        // Walk up from write to any branching construct before reaching a BlockSyntax ancestor
        // that also contains the read. If we exit a conditional branch before reaching that block,
        // the write does not dominate the read.
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

    private static bool WriteBlockChildPrecedesReadBlockChild(BlockSyntax block, StatementSyntax writeStmt, StatementSyntax readStmt)
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

    private static bool IsConditionalConstruct(SyntaxNode node)
    {
        return node is IfStatementSyntax or ElseClauseSyntax or SwitchSectionSyntax
            or SwitchStatementSyntax or DoStatementSyntax or WhileStatementSyntax
            or ForStatementSyntax or ForEachStatementSyntax or CatchClauseSyntax
            or FinallyClauseSyntax;
    }

    private static bool IsIdentifierReassignedBetween(SyntaxNode body, WriteOp write, ReadOp read)
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

    private static string? ExtractIdentifier(string key)
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
            : null;
    }

    private struct WriteOp
    {
        public WriteOp(
            SyntaxNode reportNode,
            ExpressionSyntax? factoryNode,
            string pathKey,
            ExpressionSyntax? sourceExpr,
            DiskIoCatalog.IoKind kind,
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
        public DiskIoCatalog.IoKind Kind { get; }
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

    private readonly struct ReadOp
    {
        public ReadOp(
            SyntaxNode reportNode,
            string pathKey,
            DiskIoCatalog.IoKind kind,
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
        public DiskIoCatalog.IoKind Kind { get; }
        public bool IsAsync { get; }
        public bool IsAwaited { get; }
    }
}

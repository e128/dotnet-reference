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

        var writes = new List<DiskIoCatalog.WriteOp>();
        var reads = new List<DiskIoCatalog.ReadOp>();
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
        List<DiskIoCatalog.WriteOp> writes,
        List<DiskIoCatalog.ReadOp> reads,
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
                DiskIoCatalog.TrackWriterVariable(local.Declaration, writes, writerVarToFactory, null);
                DiskIoCatalog.TryTrackOpaqueWriteResult(local.Declaration, writes);
            }
            else if (node is UsingStatementSyntax usingStmt && usingStmt.Declaration is { } decl)
            {
                DiskIoCatalog.TrackWriterVariable(decl, writes, writerVarToFactory, usingStmt);
            }
        }
    }

    private static void TryClassifyInvocation(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        List<DiskIoCatalog.WriteOp> writes,
        List<DiskIoCatalog.ReadOp> reads,
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
        List<DiskIoCatalog.WriteOp> writes,
        List<DiskIoCatalog.ReadOp> reads)
    {
        if (!DiskIoCatalog.IsReceiverType(receiver, "File"))
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

    private static void AddFileValueWrite(InvocationExpressionSyntax invocation, string methodName, List<DiskIoCatalog.WriteOp> writes)
    {
        var args = invocation.ArgumentList.Arguments;
        if (args.Count < 2)
        {
            return;
        }

        writes.Add(new DiskIoCatalog.WriteOp(
            invocation,
            null,
            DiskIoCatalog.Normalize(args[0].Expression),
            args[1].Expression,
            DiskIoCatalog.FileMethodKind(methodName),
            DiskIoCatalog.IsAsyncName(methodName),
            false,
            invocation.SpanStart));
    }

    private static void AddFileValueRead(InvocationExpressionSyntax invocation, string methodName, List<DiskIoCatalog.ReadOp> reads)
    {
        var args = invocation.ArgumentList.Arguments;
        if (args.Count < 1)
        {
            return;
        }

        reads.Add(new DiskIoCatalog.ReadOp(
            DiskIoCatalog.GetAwaitedNodeOrSelf(invocation),
            DiskIoCatalog.Normalize(args[0].Expression),
            DiskIoCatalog.FileMethodKind(methodName),
            DiskIoCatalog.IsAsyncName(methodName),
            DiskIoCatalog.IsInsideAwait(invocation)));
    }

    private static void AddFileWriteFactory(InvocationExpressionSyntax invocation, string methodName, List<DiskIoCatalog.WriteOp> writes)
    {
        var args = invocation.ArgumentList.Arguments;
        if (args.Count < 1)
        {
            return;
        }

        writes.Add(new DiskIoCatalog.WriteOp(
            invocation,
            invocation,
            DiskIoCatalog.Normalize(args[0].Expression),
            null,
            DiskIoCatalog.FileMethodKind(methodName),
            false,
            true,
            int.MaxValue));
    }

    private static void AddFileReadFactory(InvocationExpressionSyntax invocation, string methodName, List<DiskIoCatalog.ReadOp> reads)
    {
        var args = invocation.ArgumentList.Arguments;
        if (args.Count < 1)
        {
            return;
        }

        reads.Add(new DiskIoCatalog.ReadOp(
            invocation,
            DiskIoCatalog.Normalize(args[0].Expression),
            DiskIoCatalog.FileMethodKind(methodName),
            false,
            false));
    }

    private static bool TryHandleFileInfoInstance(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        ExpressionSyntax receiver,
        string methodName,
        List<DiskIoCatalog.WriteOp> writes,
        List<DiskIoCatalog.ReadOp> reads)
    {
        if (!IsFileInfoReceiver(context, receiver))
        {
            return false;
        }

        if (DiskIoCatalog.IsFileWriteFactory(methodName))
        {
            writes.Add(new DiskIoCatalog.WriteOp(
                invocation,
                invocation,
                DiskIoCatalog.NormalizeFileInfoInstance(receiver),
                null,
                DiskIoCatalog.FileMethodKind(methodName),
                false,
                true,
                int.MaxValue));
            return true;
        }

        if (DiskIoCatalog.IsFileReadFactory(methodName))
        {
            reads.Add(new DiskIoCatalog.ReadOp(
                invocation,
                DiskIoCatalog.NormalizeFileInfoInstance(receiver),
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
        List<DiskIoCatalog.WriteOp> writes,
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
        List<DiskIoCatalog.WriteOp> writes,
        List<DiskIoCatalog.ReadOp> reads)
    {
        var typeName = DiskIoCatalog.ExtractCtorTypeName(creation.Type);
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
        List<DiskIoCatalog.WriteOp> writes)
    {
        writes.Add(new DiskIoCatalog.WriteOp(
            creation,
            creation,
            DiskIoCatalog.Normalize(args[0].Expression),
            null,
            DiskIoCatalog.IoKind.Writer,
            false,
            true,
            int.MaxValue));
    }

    private static void AddStreamReaderCtor(
        ObjectCreationExpressionSyntax creation,
        SeparatedSyntaxList<ArgumentSyntax> args,
        List<DiskIoCatalog.ReadOp> reads)
    {
        reads.Add(new DiskIoCatalog.ReadOp(
            creation,
            DiskIoCatalog.Normalize(args[0].Expression),
            DiskIoCatalog.IoKind.Reader,
            false,
            false));
    }

    private static void AddFileStreamCtor(
        ObjectCreationExpressionSyntax creation,
        SeparatedSyntaxList<ArgumentSyntax> args,
        List<DiskIoCatalog.WriteOp> writes,
        List<DiskIoCatalog.ReadOp> reads)
    {
        var mode = args.Count >= 2 ? args[1].Expression : null;
        var access = args.Count >= 3 ? args[2].Expression : null;
        var isWrite = DiskIoCatalog.IsFileStreamWriteIntent(mode, access);
        if (isWrite)
        {
            writes.Add(new DiskIoCatalog.WriteOp(
                creation,
                creation,
                DiskIoCatalog.Normalize(args[0].Expression),
                null,
                DiskIoCatalog.IoKind.Stream,
                false,
                true,
                int.MaxValue));
        }
        else
        {
            reads.Add(new DiskIoCatalog.ReadOp(
                creation,
                DiskIoCatalog.Normalize(args[0].Expression),
                DiskIoCatalog.IoKind.Stream,
                false,
                false));
        }
    }

    private static void AddBinaryWriterCtor(
        ObjectCreationExpressionSyntax creation,
        SeparatedSyntaxList<ArgumentSyntax> args,
        List<DiskIoCatalog.WriteOp> writes)
    {
        var innerWritePath = DiskIoCatalog.ExtractPathFromStreamArg(args[0].Expression);
        if (innerWritePath is not null)
        {
            writes.Add(new DiskIoCatalog.WriteOp(
                creation,
                creation,
                DiskIoCatalog.Normalize(innerWritePath),
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
        List<DiskIoCatalog.ReadOp> reads)
    {
        var innerReadPath = DiskIoCatalog.ExtractPathFromStreamArg(args[0].Expression);
        if (innerReadPath is not null)
        {
            reads.Add(new DiskIoCatalog.ReadOp(
                creation,
                DiskIoCatalog.Normalize(innerReadPath),
                DiskIoCatalog.IoKind.Binary,
                false,
                false));
        }
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

    private static void Correlate(
        SyntaxNodeAnalysisContext context,
        SyntaxNode body,
        List<DiskIoCatalog.WriteOp> writes,
        List<DiskIoCatalog.ReadOp> reads)
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

    private static DiskIoCatalog.WriteOp? FindMatchingWrite(
        SyntaxNode body,
        List<DiskIoCatalog.WriteOp> writes,
        DiskIoCatalog.ReadOp read,
        StatementSyntax readStmt)
    {
        for (var i = writes.Count - 1; i >= 0; i--)
        {
            var write = writes[i];
            if (!DiskIoCatalog.KeysOverlap(write.PathKey, read.PathKey))
            {
                continue;
            }

            var writeStmt = write.ReportNode.FirstAncestorOrSelf<StatementSyntax>();
            if (writeStmt is null)
            {
                continue;
            }

            if (!DiskIoCatalog.WriteLinearlyPrecedesRead(writeStmt, readStmt))
            {
                continue;
            }

            if (DiskIoCatalog.IsIdentifierReassignedBetween(body, write, read))
            {
                continue;
            }

            return write;
        }

        return null;
    }

    private static void ReportRoundtrip(SyntaxNodeAnalysisContext context, DiskIoCatalog.ReadOp read, DiskIoCatalog.WriteOp match)
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
}

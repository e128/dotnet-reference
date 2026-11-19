using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128239: A <c lang="csharp">Stream.Read</c> call is used as a statement, so its byte
///     count is discarded, and the same buffer is read later.
/// </summary>
/// <remarks>
///     A stream can return fewer bytes than the buffer length. The later read then consumes
///     stale bytes past the returned count. The rule reports only when a later statement
///     reads the buffer, so a genuinely unused read stays silent. A reassignment of the
///     buffer ends the scan, because the later read then sees the new value. The rule skips
///     a lambda and a local function body, whose execution time the statement order does not
///     describe.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DiscardedStreamReadAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128239";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Stream.Read count is discarded",
        "Stream.Read can return fewer bytes than requested; use the returned count before reading the buffer",
        "E128-Reliability",
        DiagnosticSeverity.Warning,
        true,
        "Store the returned count and consume only that many bytes. A partial read leaves the rest of the buffer unchanged.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static start =>
        {
            if (start.Compilation.GetTypeByMetadataName("System.IO.Stream") is not { } streamType)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => Analyze(nodeContext, streamType), SyntaxKind.ExpressionStatement);
        });
    }

    private static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol streamType)
    {
        if (TryMatchRead(context, streamType) is not { } match)
        {
            return;
        }

        if (!ReadsBufferAfter(match.Block, match.Statement, match.Buffer, context))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, match.Invocation.GetLocation()));
    }

    private static (InvocationExpressionSyntax Invocation, BlockSyntax Block, ExpressionStatementSyntax Statement,
        ISymbol Buffer)? TryMatchRead(SyntaxNodeAnalysisContext context, INamedTypeSymbol streamType)
    {
        var statement = (ExpressionStatementSyntax)context.Node;
        if (statement.Expression is not InvocationExpressionSyntax invocation)
        {
            return null;
        }

        if (statement.Parent is not BlockSyntax block)
        {
            return null;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !IsStreamRead(method, streamType))
        {
            return null;
        }

        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count is 0)
        {
            return null;
        }

        var buffer = UnwrapBuffer(arguments[0].Expression);
        return context.SemanticModel.GetSymbolInfo(buffer, context.CancellationToken).Symbol is { } symbol
            ? (invocation, block, statement, symbol)
            : null;
    }

    private static bool IsStreamRead(IMethodSymbol method, INamedTypeSymbol streamType)
    {
        return string.Equals(method.Name, "Read", StringComparison.Ordinal)
            && !method.IsStatic
            && method.ReturnType.SpecialType is SpecialType.System_Int32
            && TypeSymbols.DerivesFrom(method.ContainingType, streamType);
    }

    /// <summary>
    ///     Returns the storage behind a span argument, so <c lang="csharp">buffer.AsSpan()</c>
    ///     and a plain <c lang="csharp">buffer</c> key on the same symbol. Any other expression
    ///     stays as it is, and its own symbol decides the match.
    /// </summary>
    private static ExpressionSyntax UnwrapBuffer(ExpressionSyntax expression)
    {
        if (expression is not InvocationExpressionSyntax invocation
            || invocation.Expression is not MemberAccessExpressionSyntax access)
        {
            return expression;
        }

        return string.Equals(access.Name.Identifier.ValueText, "AsSpan", StringComparison.Ordinal)
            ? access.Expression
            : expression;
    }

    /// <summary>
    ///     True when a later statement in the same block reads the buffer. A reassignment of the
    ///     buffer ends the scan with no report, because the later read then sees the new value.
    /// </summary>
    private static bool ReadsBufferAfter(
        BlockSyntax block,
        ExpressionStatementSyntax statement,
        ISymbol bufferSymbol,
        SyntaxNodeAnalysisContext context)
    {
        var seen = false;
        foreach (var sibling in block.Statements)
        {
            if (!seen)
            {
                seen = sibling == statement;
                continue;
            }

            var result = Scan(sibling, bufferSymbol, context);
            if (result is BufferUse.Reassigned)
            {
                return false;
            }

            if (result is BufferUse.Read)
            {
                return true;
            }
        }

        return false;
    }

    private enum BufferUse
    {
        None = 0,
        Read = 1,
        Reassigned = 2
    }

    private static BufferUse Scan(SyntaxNode statement, ISymbol bufferSymbol, SyntaxNodeAnalysisContext context)
    {
        foreach (var node in statement.DescendantNodesAndSelf(Descend))
        {
            if (node is not IdentifierNameSyntax identifier)
            {
                continue;
            }

            if (!SymbolEqualityComparer.Default.Equals(
                    context.SemanticModel.GetSymbolInfo(identifier, context.CancellationToken).Symbol,
                    bufferSymbol))
            {
                continue;
            }

            return IsReassigned(identifier) ? BufferUse.Reassigned : BufferUse.Read;
        }

        return BufferUse.None;
    }

    /// <summary>
    ///     Descends through every node except a lambda and a local function. The statement order
    ///     does not describe when those bodies run.
    /// </summary>
    private static bool Descend(SyntaxNode node)
    {
        return node is not (AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax);
    }

    /// <summary>
    ///     True when the identifier is the left side of an assignment, directly or through a
    ///     receiver such as <c lang="csharp">this._buffer</c>.
    /// </summary>
    private static bool IsReassigned(IdentifierNameSyntax identifier)
    {
        return identifier.Parent switch
        {
            AssignmentExpressionSyntax assignment => assignment.Left == identifier,
            MemberAccessExpressionSyntax { Parent: AssignmentExpressionSyntax assignment } access =>
                assignment.Left == access,
            _ => false
        };
    }
}

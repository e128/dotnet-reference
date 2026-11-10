using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128162: Reports a read of a local after the same block calls
///     <c lang="csharp">Dispose()</c> on it. The object throws
///     <c lang="csharp">ObjectDisposedException</c> on the next member call that needs its state.
/// </summary>
/// <remarks>
///     The rule reads an explicit call in one block. A dispose at the end of a
///     <c lang="csharp">using</c> scope stays out, because the scope holds no later statement.
///     A read inside a nested lambda or local function stays out, because the block
///     order does not give the time that body runs.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UseAfterDisposeAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128162";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Local used after an explicit Dispose",
        "'{0}' is used after it is disposed",
        "E128-Reliability",
        DiagnosticSeverity.Warning,
        true,
        "An object that Dispose already released throws ObjectDisposedException on the next " +
        "member call that reads its state. Reach the value you need before the Dispose call, or " +
        "let a using statement end the scope.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeDispose, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeDispose(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax
            {
                Name.Identifier.ValueText: "Dispose",
                Expression: IdentifierNameSyntax receiver
            }
            || invocation.ArgumentList.Arguments.Count is not 0)
        {
            return;
        }

        var model = context.SemanticModel;

        if (model.GetSymbolInfo(receiver, context.CancellationToken).Symbol is not ILocalSymbol local)
        {
            return;
        }

        // Only a Dispose statement that sits directly in a block runs on every path through that
        // block. A braceless if, else, loop, or switch section runs on some paths only.
        if (invocation.Parent is not ExpressionStatementSyntax { Parent: BlockSyntax block })
        {
            return;
        }

        var usage = FindUseAfterDispose(block, local, model, invocation.Span.End, context.CancellationToken);
        if (usage is null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, usage.GetLocation(), local.Name));
    }

    private static IdentifierNameSyntax? FindUseAfterDispose(
        BlockSyntax block,
        ILocalSymbol local,
        SemanticModel model,
        int disposeEnd,
        CancellationToken cancellationToken)
    {
        foreach (var identifier in block.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            if (!IsReadOfLocal(identifier, local, model, disposeEnd, block, cancellationToken))
            {
                continue;
            }

            // A new assignment ends the disposed state. Stop the scan instead of reporting the reads
            // that follow, because they read a fresh object.
            if (identifier.Parent is AssignmentExpressionSyntax assignment
                && assignment.Left == identifier)
            {
                return null;
            }

            // A second Dispose is harmless, so the rule does not report it. Only a member access on the
            // local reads its state. A nameof, a null test, and a comparison do not.
            if (!IsDisposeReceiver(identifier) && IsMemberAccessReceiver(identifier))
            {
                return identifier;
            }
        }

        return null;
    }

    private static bool IsReadOfLocal(
        IdentifierNameSyntax identifier,
        ILocalSymbol local,
        SemanticModel model,
        int disposeEnd,
        BlockSyntax block,
        CancellationToken cancellationToken)
    {
        // A nested function body runs at a time the block order does not give. A local function can
        // run before the Dispose call that its body follows in the source text.
        return identifier.SpanStart >= disposeEnd
               && !IsInsideNestedFunction(identifier, block)
               && SymbolEqualityComparer.Default.Equals(
                   model.GetSymbolInfo(identifier, cancellationToken).Symbol,
                   local);
    }

    private static bool IsInsideNestedFunction(SyntaxNode node, BlockSyntax block)
    {
        for (var current = node.Parent; current is not null && current != block; current = current.Parent)
        {
            if (current is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsMemberAccessReceiver(IdentifierNameSyntax identifier)
    {
        if (identifier.Parent is not MemberAccessExpressionSyntax access || access.Expression != identifier)
        {
            return false;
        }

        foreach (var ancestor in identifier.Ancestors())
        {
            if (ancestor is InvocationExpressionSyntax { Expression: IdentifierNameSyntax nameofName }
                && string.Equals(nameofName.Identifier.ValueText, "nameof", StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsDisposeReceiver(IdentifierNameSyntax identifier)
    {
        return identifier.Parent is MemberAccessExpressionSyntax access
               && string.Equals(access.Name.Identifier.ValueText, "Dispose", StringComparison.Ordinal)
               && access.Parent is InvocationExpressionSyntax;
    }
}

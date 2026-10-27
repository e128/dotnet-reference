using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128110: Return on an ArrayPool buffer must follow CryptographicOperations.ZeroMemory of the
///     whole rented array, in the same finally block. A clearArray argument of <see langword="true" /> does
///     not satisfy the rule.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PooledBufferZeroingAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128110";
    internal const string UseAfterReturnDiagnosticId = "E128111";

    private const string ArrayPoolMetadataName = "System.Buffers.ArrayPool`1";
    private const string CryptographicOperationsMetadataName = "System.Security.Cryptography.CryptographicOperations";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Zero a pooled buffer in a finally block before Return",
        "Zero '{0}' with CryptographicOperations.ZeroMemory (whole array) in the same finally block before Return",
        "Reliability",
        DiagnosticSeverity.Warning,
        true,
        "A rented array is oversized and keeps its old contents. Call CryptographicOperations.ZeroMemory on the " +
        "whole array in a finally block so every exit path clears it. Return(buffer, clearArray: true) is not " +
        "the required guarantee.");

    private static readonly DiagnosticDescriptor UseAfterReturnRule = new(
        UseAfterReturnDiagnosticId,
        "Do not use a pooled buffer after Return",
        "'{0}' is used after it is returned to the pool",
        "Reliability",
        DiagnosticSeverity.Warning,
        true,
        "After Return the pool may hand the array to another caller. A later read or write is a use-after-free. " +
        "Return from a finally block only when no other thread or pending async operation still uses the buffer. " +
        "Abandoning the buffer is safer than a use-after-free.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule, UseAfterReturnRule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var poolType = compilationContext.Compilation.GetTypeByMetadataName(ArrayPoolMetadataName);
            if (poolType is null)
            {
                return;
            }

            var cryptoType = compilationContext.Compilation.GetTypeByMetadataName(CryptographicOperationsMetadataName);

            compilationContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeInvocation(nodeContext, poolType, cryptoType),
                SyntaxKind.InvocationExpression);
        });
    }

    private static void AnalyzeInvocation(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol poolType,
        INamedTypeSymbol? cryptoType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess
            || !string.Equals(memberAccess.Name.Identifier.ValueText, "Return", StringComparison.Ordinal))
        {
            return;
        }

        var args = invocation.ArgumentList.Arguments;
        if (args.Count is < 1 or > 2)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType?.OriginalDefinition, poolType))
        {
            return;
        }

        var model = context.SemanticModel;
        var bufferSymbol = model.GetSymbolInfo(args[0].Expression, context.CancellationToken).Symbol;
        var finallyClause = FindEnclosingFinally(invocation);

        if (bufferSymbol is not null)
        {
            var use = FindUseAfterReturn(invocation, bufferSymbol, model, context.CancellationToken);
            if (use is not null)
            {
                context.ReportDiagnostic(Diagnostic.Create(UseAfterReturnRule, use.GetLocation(), bufferSymbol.Name));
            }
        }

        if (bufferSymbol is not null
            && cryptoType is not null
            && finallyClause is not null
            && ZeroesWholeBufferBefore(finallyClause, invocation, bufferSymbol, cryptoType, model, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            invocation.GetLocation(),
            args[0].Expression.ToString()));
    }

    // Scan the statements that follow the Return statement in each enclosing block. Stop at a reassignment.
    private static IdentifierNameSyntax? FindUseAfterReturn(
        InvocationExpressionSyntax returnCall,
        ISymbol bufferSymbol,
        SemanticModel model,
        System.Threading.CancellationToken cancellationToken)
    {
        SyntaxNode current = returnCall;
        foreach (var ancestor in returnCall.Ancestors())
        {
            if (ancestor is MemberDeclarationSyntax or AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax)
            {
                return null;
            }

            if (ancestor is BlockSyntax block && current is StatementSyntax statement)
            {
                var index = block.Statements.IndexOf(statement);
                for (var i = index + 1; i < block.Statements.Count; i++)
                {
                    var next = block.Statements[i];
                    if (Reassigns(next, bufferSymbol, model, cancellationToken))
                    {
                        return null;
                    }

                    var use = next.DescendantNodesAndSelf()
                        .OfType<IdentifierNameSyntax>()
                        .FirstOrDefault(name => SymbolEqualityComparer.Default.Equals(
                            model.GetSymbolInfo(name, cancellationToken).Symbol,
                            bufferSymbol));
                    if (use is not null)
                    {
                        return use;
                    }
                }
            }

            current = ancestor;
        }

        return null;
    }

    private static bool Reassigns(
        StatementSyntax statement,
        ISymbol bufferSymbol,
        SemanticModel model,
        System.Threading.CancellationToken cancellationToken)
    {
        return statement is ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assignment }
               && SymbolEqualityComparer.Default.Equals(
                   model.GetSymbolInfo(assignment.Left, cancellationToken).Symbol,
                   bufferSymbol);
    }

    private static FinallyClauseSyntax? FindEnclosingFinally(SyntaxNode node)
    {
        foreach (var ancestor in node.Ancestors())
        {
            if (ancestor is FinallyClauseSyntax finallyClause)
            {
                return finallyClause;
            }

            if (ancestor is MemberDeclarationSyntax or AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax)
            {
                return null;
            }
        }

        return null;
    }

    private static bool ZeroesWholeBufferBefore(
        FinallyClauseSyntax finallyClause,
        InvocationExpressionSyntax returnCall,
        ISymbol bufferSymbol,
        INamedTypeSymbol cryptoType,
        SemanticModel model,
        System.Threading.CancellationToken cancellationToken)
    {
        // Only a direct statement of the finally block counts. A ZeroMemory nested in an if, loop or
        // lambda may not run on every path.
        return finallyClause.Block.Statements
            .OfType<ExpressionStatementSyntax>()
            .Select(statement => statement.Expression)
            .OfType<InvocationExpressionSyntax>()
            .Any(call => call.Span.End <= returnCall.SpanStart
                         && IsZeroMemoryOf(call, bufferSymbol, cryptoType, model, cancellationToken));
    }

    private static bool IsZeroMemoryOf(
        InvocationExpressionSyntax call,
        ISymbol bufferSymbol,
        INamedTypeSymbol cryptoType,
        SemanticModel model,
        System.Threading.CancellationToken cancellationToken)
    {
        if (call.ArgumentList.Arguments.Count != 1
            || model.GetSymbolInfo(call, cancellationToken).Symbol is not IMethodSymbol
            {
                Name: "ZeroMemory"
            } zeroMethod
            || !SymbolEqualityComparer.Default.Equals(zeroMethod.ContainingType, cryptoType))
        {
            return false;
        }

        var target = Unwrap(call.ArgumentList.Arguments[0].Expression);
        return SymbolEqualityComparer.Default.Equals(
            model.GetSymbolInfo(target, cancellationToken).Symbol,
            bufferSymbol);
    }

    // Peel buffer.AsSpan() and MemoryMarshal.AsBytes(x). A slice such as AsSpan(0, n) stays wrapped,
    // so it does not match the whole-array symbol.
    private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
    {
        while (expression is InvocationExpressionSyntax invocation)
        {
            if (invocation.Expression is MemberAccessExpressionSyntax access
                && string.Equals(access.Name.Identifier.ValueText, "AsSpan", StringComparison.Ordinal)
                && invocation.ArgumentList.Arguments.Count == 0)
            {
                expression = access.Expression;
            }
            else if (invocation.Expression is MemberAccessExpressionSyntax asBytes
                     && string.Equals(asBytes.Name.Identifier.ValueText, "AsBytes", StringComparison.Ordinal)
                     && invocation.ArgumentList.Arguments.Count == 1)
            {
                expression = invocation.ArgumentList.Arguments[0].Expression;
            }
            else
            {
                break;
            }
        }

        return expression;
    }
}

using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128183: Reports a <c lang="csharp">Task.WhenAny</c> call whose result is never observed. The
///     task returned by <c lang="csharp">Task.WhenAny</c> completes when any one task in the set
///     completes. It never completes faulted, so a fault in every task in the set stays unobserved.
/// </summary>
/// <remarks>
///     The rule reports an expression statement that discards the result, and a local that the rest of
///     the block never reads. A result that is awaited, returned, yielded, passed as an argument, or
///     stored in a field, a property, or a collection stays silent because the caller may observe it
///     there.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class WhenAnyResultUnobservedAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128183";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Task.WhenAny result is never awaited",
        "The task returned by 'Task.WhenAny' is never awaited, so an exception from every task in the set goes unobserved",
        "E128-Reliability",
        DiagnosticSeverity.Warning,
        true,
        "The task returned by Task.WhenAny completes when any one task in the set completes. It " +
        "never completes faulted, so a fault in every task in the set stays unobserved. Await the " +
        "returned task, then await the completed task again to observe its exception.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start =>
            MetadataTypes.RegisterForType(
                start,
                "System.Threading.Tasks.Task",
                AnalyzeInvocation,
                SyntaxKind.InvocationExpression));
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol taskType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (!IsTaskWhenAny(context, invocation, taskType))
        {
            return;
        }

        if (IsDiscardedStatement(invocation))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
            return;
        }

        AnalyzeLocalAssignment(context, invocation);
    }

    private static bool IsTaskWhenAny(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol taskType)
    {
        // The name check drops almost every invocation before the symbol lookup runs. The symbol
        // decides the match, so a user method that carries the name WhenAny stays silent.
        var name = invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            _ => null
        };

        if (!string.Equals(name, "WhenAny", StringComparison.Ordinal))
        {
            return false;
        }

        var symbol = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol;

        return symbol is IMethodSymbol method
               && string.Equals(method.Name, "WhenAny", StringComparison.Ordinal)
               && SymbolEqualityComparer.Default.Equals(method.ContainingType, taskType);
    }

    // Shape one: the call is the whole expression statement, so the statement discards the result.
    private static bool IsDiscardedStatement(InvocationExpressionSyntax invocation)
    {
        return invocation.Parent is ExpressionStatementSyntax;
    }

    // Shape two: the result goes into a local, and the rest of the enclosing block never reads the
    // local. A field or a property keeps the result reachable, so only a local is reported.
    private static void AnalyzeLocalAssignment(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation)
    {
        if (invocation.Parent is not EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator })
        {
            return;
        }

        if (declarator.Parent?.Parent is not LocalDeclarationStatementSyntax declaration)
        {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(declarator, context.CancellationToken)
            is not ILocalSymbol local)
        {
            return;
        }

        if (IsLocalRead(context, declaration, local))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
    }

    // One block is the whole scope of the analysis. Every read of the local in that block, whatever
    // its shape, hands the task to something that can observe it. Await, return, yield, an argument,
    // a field, and a collection each appear as a read, so the rule reports only a local with none.
    private static bool IsLocalRead(
        SyntaxNodeAnalysisContext context,
        LocalDeclarationStatementSyntax declaration,
        ILocalSymbol local)
    {
        var scope = declaration.FirstAncestorOrSelf<BlockSyntax>();

        // A declaration outside a block has no bounded scope to inspect. Stay silent.
        if (scope is null)
        {
            return true;
        }

        foreach (var identifier in scope.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            if (!string.Equals(identifier.Identifier.ValueText, local.Name, StringComparison.Ordinal))
            {
                continue;
            }

            var symbol = context.SemanticModel.GetSymbolInfo(identifier, context.CancellationToken).Symbol;

            if (SymbolEqualityComparer.Default.Equals(symbol, local))
            {
                return true;
            }
        }

        return false;
    }
}

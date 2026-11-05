using System;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128193: Reports an assignment that overwrites a disposable field inside a loop when no
///     earlier <c lang="csharp">Dispose</c> or <c lang="csharp">DisposeAsync</c> call releases the
///     previous value.
/// </summary>
/// <remarks>
///     The loop replaces the field value on each iteration, and the earlier value never reaches a
///     dispose call. The rule reads <c lang="csharp">System.IDisposable</c> and
///     <c lang="csharp">System.IAsyncDisposable</c>. A dispose call counts when its receiver
///     resolves to the same field symbol and starts before the assignment in the enclosing method,
///     constructor, or accessor.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DisposableFieldInLoopAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128193";

    internal const string DisposableTypeName = "System.IDisposable";

    internal const string AsyncDisposableTypeName = "System.IAsyncDisposable";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Disposable field overwritten in a loop without a dispose",
        "'{0}' overwrites a disposable field inside a loop, and no dispose call on that field precedes it",
        "E128-Reliability",
        DiagnosticSeverity.Warning,
        true,
        "The loop replaces the field value on each iteration, and the earlier value never reaches " +
        "a dispose call. Dispose the field before the assignment, or hold the value in a local " +
        "and dispose the local at the end of the iteration.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var disposableTypes = MetadataTypes.Find(context.Compilation, DisposableTypeName);
        var asyncDisposableTypes = MetadataTypes.Find(context.Compilation, AsyncDisposableTypeName);

        // Without either interface no type in the compilation can be disposable, so the rule stays silent.
        if (disposableTypes.IsEmpty && asyncDisposableTypes.IsEmpty)
        {
            return;
        }

        context.RegisterSyntaxNodeAction(
            nodeContext => AnalyzeAssignment(nodeContext, disposableTypes, asyncDisposableTypes),
            SyntaxKind.SimpleAssignmentExpression);
    }

    private static void AnalyzeAssignment(
        SyntaxNodeAnalysisContext context,
        ImmutableArray<INamedTypeSymbol> disposableTypes,
        ImmutableArray<INamedTypeSymbol> asyncDisposableTypes)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;

        // A local, a parameter, or a property has no field symbol, so only a field reaches the check.
        if (context.SemanticModel.GetSymbolInfo(assignment.Left, context.CancellationToken).Symbol
            is not IFieldSymbol field)
        {
            return;
        }

        if (!IsDisposable(field.Type, disposableTypes, asyncDisposableTypes))
        {
            return;
        }

        if (FindEnclosingLoop(assignment) is not { } loop)
        {
            return;
        }

        if (HasEarlierDispose(loop, assignment, field, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, assignment.Left.GetLocation(), field.Name));
    }

    private static bool IsDisposable(
        ITypeSymbol type,
        ImmutableArray<INamedTypeSymbol> disposableTypes,
        ImmutableArray<INamedTypeSymbol> asyncDisposableTypes)
    {
        if (type is ITypeParameterSymbol typeParameter)
        {
            // A constraint such as "where T : IDisposable" makes the field disposable.
            foreach (var constraint in typeParameter.ConstraintTypes)
            {
                if (IsDisposable(constraint, disposableTypes, asyncDisposableTypes))
                {
                    return true;
                }
            }

            return false;
        }

        if (type.IsValueType || type is not INamedTypeSymbol namedType)
        {
            return false;
        }

        if (MetadataTypes.Contains(disposableTypes, namedType.OriginalDefinition)
            || MetadataTypes.Contains(asyncDisposableTypes, namedType.OriginalDefinition))
        {
            return true;
        }

        foreach (var implemented in namedType.AllInterfaces)
        {
            if (MetadataTypes.Contains(disposableTypes, implemented.OriginalDefinition)
                || MetadataTypes.Contains(asyncDisposableTypes, implemented.OriginalDefinition))
            {
                return true;
            }
        }

        return false;
    }

    private static SyntaxNode? FindEnclosingLoop(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (current is ForStatementSyntax or CommonForEachStatementSyntax or WhileStatementSyntax or DoStatementSyntax)
            {
                // A loop source, condition, initializer, or incrementor runs outside the body.
                if (!IsInLoopHeader(current, node))
                {
                    return current;
                }

                continue;
            }

            // A lambda or a local function runs on its own schedule, so the enclosing loop does not
            // make the assignment a per-iteration write.
            if (current is MethodDeclarationSyntax or ConstructorDeclarationSyntax or AccessorDeclarationSyntax
                or LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax or BaseTypeDeclarationSyntax)
            {
                return null;
            }
        }

        return null;
    }

    private static bool IsInLoopHeader(SyntaxNode loop, SyntaxNode node)
    {
        return (loop is CommonForEachStatementSyntax forEach && forEach.Expression.Span.Contains(node.Span))
               || (loop is ForStatementSyntax forStatement
                   && (ContainsSpan(forStatement.Declaration, node)
                       || ContainsSpan(forStatement.Condition, node)
                       || IsInAny(forStatement.Initializers, node)
                       || IsInAny(forStatement.Incrementors, node)))
               || (loop is WhileStatementSyntax whileStatement && whileStatement.Condition.Span.Contains(node.Span))
               || (loop is DoStatementSyntax doStatement && doStatement.Condition.Span.Contains(node.Span));
    }

    private static bool ContainsSpan(SyntaxNode? container, SyntaxNode node)
    {
        return container is not null && container.Span.Contains(node.Span);
    }

    private static bool IsInAny(SeparatedSyntaxList<ExpressionSyntax> expressions, SyntaxNode node)
    {
        foreach (var expression in expressions)
        {
            if (expression.Span.Contains(node.Span))
            {
                return true;
            }
        }

        return false;
    }

    // Only a dispose inside the same loop releases the value that the previous iteration stored.
    // A dispose before the loop releases the first value only.
    private static bool HasEarlierDispose(
        SyntaxNode loop,
        AssignmentExpressionSyntax assignment,
        IFieldSymbol field,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        foreach (var node in loop.DescendantNodes())
        {
            if (node is not InvocationExpressionSyntax invocation || invocation.SpanStart >= assignment.SpanStart)
            {
                continue;
            }

            if (DisposeCalls.GetReceiver(invocation) is not { } receiver)
            {
                continue;
            }

            if (model.GetSymbolInfo(receiver, cancellationToken).Symbol is IFieldSymbol disposedField
                && SymbolEqualityComparer.Default.Equals(disposedField, field))
            {
                return true;
            }
        }

        return false;
    }

}

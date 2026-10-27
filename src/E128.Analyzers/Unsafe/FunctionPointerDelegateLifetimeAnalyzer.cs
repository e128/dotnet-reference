using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Unsafe;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FunctionPointerDelegateLifetimeAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128128";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Keep the delegate alive while native code holds its function pointer",
        "Marshal.GetFunctionPointerForDelegate does not root the delegate. Native code can call a collected delegate.",
        "Unsafe",
        DiagnosticSeverity.Warning,
        true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var marshal = MetadataTypes.Find(start.Compilation, "System.Runtime.InteropServices.Marshal");
            if (marshal.IsEmpty)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(
                node => Analyze(node, marshal),
                SyntaxKind.InvocationExpression);
        });
    }

    private static void Analyze(SyntaxNodeAnalysisContext context, ImmutableArray<INamedTypeSymbol> marshal)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (InvocationNames.GetSimpleName(invocation) is not { } memberAccess
            || !string.Equals(
                memberAccess.Identifier.ValueText,
                "GetFunctionPointerForDelegate",
                StringComparison.Ordinal)
            || invocation.ArgumentList.Arguments.Count != 1)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !MetadataTypes.Contains(marshal, method.ContainingType))
        {
            return;
        }

        if (!IsUnrootedDelegate(invocation.ArgumentList.Arguments[0].Expression, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
    }

    private static bool IsUnrootedDelegate(
        ExpressionSyntax expression,
        SemanticModel model,
        System.Threading.CancellationToken cancellationToken)
    {
        var inner = Unwrap(expression);

        var symbol = model.GetSymbolInfo(inner, cancellationToken).Symbol;

        // A temporary delegate has no owner, so nothing keeps it alive after the call.
        return inner is ObjectCreationExpressionSyntax or ImplicitObjectCreationExpressionSyntax or AnonymousFunctionExpressionSyntax
            || symbol is IMethodSymbol
            || (symbol is ILocalSymbol or IParameterSymbol && !IsRooted(inner, model, cancellationToken));
    }

    private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
    {
        while (true)
        {
            switch (expression)
            {
                case ParenthesizedExpressionSyntax parenthesized:
                    expression = parenthesized.Expression;
                    break;
                case CastExpressionSyntax cast:
                    expression = cast.Expression;
                    break;
                default:
                    return expression;
            }
        }
    }

    // A local or parameter is safe when it is stored in a field, property or element, or passed to another call.
    private static bool IsRooted(
        ExpressionSyntax delegateExpression,
        SemanticModel model,
        System.Threading.CancellationToken cancellationToken)
    {
        var symbol = model.GetSymbolInfo(delegateExpression, cancellationToken).Symbol;
        var scope = delegateExpression.FirstAncestorOrSelf<MemberDeclarationSyntax>();
        if (symbol is null || scope is null)
        {
            return false;
        }

        foreach (var node in scope.DescendantNodes())
        {
            // Passing the delegate to another call (GC.KeepAlive, list.Add, a registry) hands it to an owner.
            if (node is InvocationExpressionSyntax call
                && call != delegateExpression.FirstAncestorOrSelf<InvocationExpressionSyntax>()
                && call.ArgumentList.Arguments.Any(argument => Refers(argument.Expression, symbol, model, cancellationToken)))
            {
                return true;
            }

            if (node is AssignmentExpressionSyntax assignment
                && Refers(assignment.Right, symbol, model, cancellationToken)
                && (assignment.Left is ElementAccessExpressionSyntax
                    || model.GetSymbolInfo(assignment.Left, cancellationToken).Symbol is IFieldSymbol or IPropertySymbol))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Refers(
        ExpressionSyntax expression,
        ISymbol symbol,
        SemanticModel model,
        System.Threading.CancellationToken cancellationToken)
    {
        return SymbolEqualityComparer.Default.Equals(
            model.GetSymbolInfo(Unwrap(expression), cancellationToken).Symbol,
            symbol);
    }
}

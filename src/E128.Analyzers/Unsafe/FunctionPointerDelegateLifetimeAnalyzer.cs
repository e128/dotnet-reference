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
        "E128-Unsafe",
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
        context.RegisterCompilationStartAction(static start =>
        {
            _ = MetadataTypes.TryRegisterForTypes(
                start,
                "System.Runtime.InteropServices.Marshal",
                Analyze,
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
            || invocation.ArgumentList.Arguments.Count is not 1)
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
        var inner = ExpressionUnwrapper.ParenthesesAndCasts(expression);

        var symbol = model.GetSymbolInfo(inner, cancellationToken).Symbol;

        return inner is ObjectCreationExpressionSyntax or ImplicitObjectCreationExpressionSyntax or AnonymousFunctionExpressionSyntax
            || symbol is IMethodSymbol
            || (symbol is ILocalSymbol or IParameterSymbol && !IsRooted(inner, model, cancellationToken));
    }

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
            if (KeepsAlive(node, delegateExpression, symbol, model, cancellationToken)
                || IsStoredInOwner(node, symbol, model, cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    private static bool KeepsAlive(
        SyntaxNode node,
        ExpressionSyntax delegateExpression,
        ISymbol symbol,
        SemanticModel model,
        System.Threading.CancellationToken cancellationToken)
    {
        if (node is not InvocationExpressionSyntax call
            || call == delegateExpression.FirstAncestorOrSelf<InvocationExpressionSyntax>()
            || !call.ArgumentList.Arguments.Any(argument => Refers(argument.Expression, symbol, model, cancellationToken)))
        {
            return false;
        }

        return model.GetSymbolInfo(call, cancellationToken).Symbol is IMethodSymbol method
               && IsRootingMethod(method);
    }

    private static bool IsRootingMethod(IMethodSymbol method)
    {
        return method.Name switch
        {
            "KeepAlive" => string.Equals(method.ContainingType.ToDisplayString(), "System.GC", StringComparison.Ordinal),
            "Alloc" => string.Equals(
                method.ContainingType.ToDisplayString(),
                "System.Runtime.InteropServices.GCHandle",
                StringComparison.Ordinal),
            "Add" or "TryAdd" or "Push" or "Enqueue" or "Insert" or "AddOrUpdate" => true,
            _ => false,
        };
    }

    private static bool IsStoredInOwner(
        SyntaxNode node,
        ISymbol symbol,
        SemanticModel model,
        System.Threading.CancellationToken cancellationToken)
    {
        return node is AssignmentExpressionSyntax assignment
            && Refers(assignment.Right, symbol, model, cancellationToken)
            && (assignment.Left is ElementAccessExpressionSyntax
                || model.GetSymbolInfo(assignment.Left, cancellationToken).Symbol is IFieldSymbol or IPropertySymbol);
    }

    private static bool Refers(
        ExpressionSyntax expression,
        ISymbol symbol,
        SemanticModel model,
        System.Threading.CancellationToken cancellationToken)
    {
        return SymbolEqualityComparer.Default.Equals(
            model.GetSymbolInfo(ExpressionUnwrapper.ParenthesesAndCasts(expression), cancellationToken).Symbol,
            symbol);
    }
}

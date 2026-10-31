using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Unsafe;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnsafeAsPointerEscapeAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128125";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Pin the object explicitly with fixed",
        "Unsafe.AsPointer takes no pin. The object can move while the pointer is live. Use a fixed block.",
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
            var unsafeType = MetadataTypes.Find(start.Compilation, "System.Runtime.CompilerServices.Unsafe");
            if (unsafeType.IsEmpty)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(
                node => Analyze(node, unsafeType),
                SyntaxKind.InvocationExpression);
        });
    }

    private static void Analyze(SyntaxNodeAnalysisContext context, ImmutableArray<INamedTypeSymbol> unsafeType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (InvocationNames.GetSimpleName(invocation) is not { } memberAccess
            || !string.Equals(memberAccess.Identifier.ValueText, "AsPointer", StringComparison.Ordinal))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !MetadataTypes.Contains(unsafeType, method.ContainingType))
        {
            return;
        }

        if (IsInsideFixedStatement(invocation)
            || IsStackLocation(invocation, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
    }

    // The stack never moves, so a pointer to a plain local or by-value parameter is stable.
    private static bool IsStackLocation(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        System.Threading.CancellationToken cancellationToken)
    {
        return invocation.ArgumentList.Arguments.Count == 1
            && !IsInAsyncMember(invocation)
            && model.GetSymbolInfo(invocation.ArgumentList.Arguments[0].Expression, cancellationToken).Symbol
                is ILocalSymbol { RefKind: RefKind.None } or IParameterSymbol { RefKind: RefKind.None };
    }

    // An async member moves its locals and by-value parameters into a state machine, which lives on
    // the heap and moves during a collection. Only a plain stack frame keeps the address stable.
    private static bool IsInAsyncMember(SyntaxNode node)
    {
        foreach (var ancestor in node.Ancestors())
        {
            if (ancestor is AnonymousFunctionExpressionSyntax anonymous)
            {
                return anonymous.AsyncKeyword.IsKind(SyntaxKind.AsyncKeyword);
            }

            if (ancestor is LocalFunctionStatementSyntax local)
            {
                return local.Modifiers.Any(SyntaxKind.AsyncKeyword);
            }

            if (ancestor is MethodDeclarationSyntax method)
            {
                return method.Modifiers.Any(SyntaxKind.AsyncKeyword);
            }

            if (ancestor is MemberDeclarationSyntax)
            {
                return false;
            }
        }

        return false;
    }

    private static bool IsInsideFixedStatement(SyntaxNode node)
    {
        foreach (var ancestor in node.Ancestors())
        {
            if (ancestor is FixedStatementSyntax)
            {
                return true;
            }

            if (ancestor is MemberDeclarationSyntax or AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax)
            {
                return false;
            }
        }

        return false;
    }
}

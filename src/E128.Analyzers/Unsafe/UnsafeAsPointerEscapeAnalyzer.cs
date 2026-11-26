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
        "Unsafe.AsPointer takes no pin. The target can move, or the pointer can outlive it. Use a fixed block.",
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
                MetadataTypes.Unsafe,
                Analyze,
                SyntaxKind.InvocationExpression);
        });
    }

    private static void Analyze(SyntaxNodeAnalysisContext context, ImmutableArray<INamedTypeSymbol> unsafeType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (!OwnerInvocation.TryMatch(context, unsafeType, "AsPointer", out _))
        {
            return;
        }

        if (IsInsideFixedStatement(invocation))
        {
            return;
        }

        if (IsStackLocation(invocation, context.SemanticModel, context.CancellationToken)
            && !Escapes(invocation, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
    }

    private static bool Escapes(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        System.Threading.CancellationToken cancellationToken)
    {
        foreach (var ancestor in invocation.Ancestors())
        {
            if (ancestor is ReturnStatementSyntax or ArrowExpressionClauseSyntax)
            {
                return true;
            }

            if (ancestor is AssignmentExpressionSyntax assignment
                && assignment.Right.Span.Contains(invocation.Span)
                && IsFieldOrElement(assignment.Left, model, cancellationToken))
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

    private static bool IsFieldOrElement(
        ExpressionSyntax target,
        SemanticModel model,
        System.Threading.CancellationToken cancellationToken)
    {
        return target is ElementAccessExpressionSyntax
            || model.GetSymbolInfo(target, cancellationToken).Symbol is IFieldSymbol or IPropertySymbol;
    }

    private static bool IsStackLocation(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        System.Threading.CancellationToken cancellationToken)
    {
        return invocation.ArgumentList.Arguments.Count is 1
            && !IsInAsyncMember(invocation)
            && model.GetSymbolInfo(invocation.ArgumentList.Arguments[0].Expression, cancellationToken).Symbol
                is ILocalSymbol { RefKind: RefKind.None } or IParameterSymbol { RefKind: RefKind.None };
    }

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

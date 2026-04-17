using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128041: Reports <c>JsonDocument.Parse()</c> calls where <c>RootElement</c> is accessed
///     and the element escapes the enclosing <see langword="using" /> scope (via return or out-parameter),
///     or the <c>JsonDocument</c> is not in a <see langword="using" /> scope at all.
///     <c>JsonDocument</c> owns pooled memory — accessing <c>RootElement</c> after the document
///     is disposed or finalized reads from returned-to-pool buffers.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class JsonDocumentLifetimeAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128041";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "JsonDocument.RootElement must not escape the document's using scope",
        "JsonDocument.Parse() result is not in a 'using' scope — RootElement accesses may read from returned-to-pool memory. Wrap in 'using' and call .Clone() if the element must escape.",
        "Reliability",
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
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (!IsJsonDocumentParse(context, invocation))
        {
            return;
        }

        if (IsInUsingScope(invocation))
        {
            if (TryGetUsingVariableName(invocation, out var variableName))
            {
                var containingMethod = invocation.FirstAncestorOrSelf<BaseMethodDeclarationSyntax>();
                if (containingMethod is null)
                {
                    return;
                }

                if (HasEscapingRootElement(containingMethod, variableName))
                {
                    context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
                }
            }

            return;
        }

        // Not in a using scope at all — always report.
        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
    }

    private static bool IsJsonDocumentParse(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation)
    {
        var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken);
        return symbolInfo.Symbol is IMethodSymbol method
               && string.Equals(method.Name, "Parse", StringComparison.Ordinal)
               && string.Equals(method.ContainingType?.ToDisplayString(), "System.Text.Json.JsonDocument", StringComparison.Ordinal);
    }

    private static bool IsInUsingScope(InvocationExpressionSyntax invocation)
    {
        // Using declaration form.
        if (invocation.Parent is EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax { Parent: LocalDeclarationStatementSyntax localDecl } } }
            && localDecl.UsingKeyword.IsKind(SyntaxKind.UsingKeyword))
        {
            return true;
        }

        // Using statement form.
        return invocation.Parent is EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax { Parent: UsingStatementSyntax } } };
    }

    private static bool TryGetUsingVariableName(InvocationExpressionSyntax invocation, out string variableName)
    {
        variableName = string.Empty;

        if (invocation.Parent is EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator })
        {
            variableName = declarator.Identifier.ValueText;
            return true;
        }

        return false;
    }

    private static bool HasEscapingRootElement(BaseMethodDeclarationSyntax method, string docVariableName)
    {
        foreach (var returnStatement in method.DescendantNodes().OfType<ReturnStatementSyntax>())
        {
            if (returnStatement.Expression is null)
            {
                continue;
            }

            var returnExpr = returnStatement.Expression;

            if (ReferencesRootElementWithoutClone(returnExpr, docVariableName))
            {
                return true;
            }
        }

        return method is MethodDeclarationSyntax { ExpressionBody: { } arrowBody }
               && ReferencesRootElementWithoutClone(arrowBody.Expression, docVariableName);
    }

    private static bool ReferencesRootElementWithoutClone(ExpressionSyntax expression, string docVariableName)
    {
        foreach (var memberAccess in expression.DescendantNodesAndSelf().OfType<MemberAccessExpressionSyntax>())
        {
            if (!IsRootElementAccess(memberAccess, docVariableName))
            {
                continue;
            }

            if (HasCloneCallAbove(memberAccess, expression))
            {
                continue;
            }

            if (IsConsumedByChainOrArgument(memberAccess))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool IsConsumedByChainOrArgument(MemberAccessExpressionSyntax rootElementAccess)
    {
        SyntaxNode current = rootElementAccess;

        while (current.Parent is not null)
        {
            var parent = current.Parent;

            if (parent is ArgumentSyntax)
            {
                return true;
            }

            if (parent is MemberAccessExpressionSyntax ma && ma.Expression == current)
            {
                if (ma.Parent is InvocationExpressionSyntax)
                {
                    return true;
                }

                current = ma;
                continue;
            }

            break;
        }

        return false;
    }

    private static bool IsRootElementAccess(MemberAccessExpressionSyntax memberAccess, string docVariableName)
    {
        return string.Equals(memberAccess.Name.Identifier.ValueText, "RootElement", StringComparison.Ordinal)
               && memberAccess.Expression is IdentifierNameSyntax id
               && string.Equals(id.Identifier.ValueText, docVariableName, StringComparison.Ordinal);
    }

    private static bool HasCloneCallAbove(MemberAccessExpressionSyntax rootElementAccess, ExpressionSyntax root)
    {
        SyntaxNode current = rootElementAccess;

        while (current != root && current.Parent is not null)
        {
            var parent = current.Parent;

            if (parent is MemberAccessExpressionSyntax parentMa
                && parentMa.Expression == current
                && string.Equals(parentMa.Name.Identifier.ValueText, "Clone", StringComparison.Ordinal)
                && parentMa.Parent is InvocationExpressionSyntax)
            {
                return true;
            }

            if (parent is MemberAccessExpressionSyntax chainMa && chainMa.Expression == current)
            {
                current = chainMa.Parent ?? chainMa;
                continue;
            }

            break;
        }

        return false;
    }
}

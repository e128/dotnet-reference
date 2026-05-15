using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class GetAttributeElementTypeAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128078";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "GetAttribute(\"href\") on element that does not carry href",
        "'{0}' elements do not carry 'href' — only <a>, <link>, <area>, and <base> do",
        "Reliability",
        DiagnosticSeverity.Warning,
        true,
        "Calling .GetAttribute(\"href\") on a non-anchor/link element silently returns null. " +
        "Only <a>, <link>, <area>, and <base> HTML elements carry the href attribute.");

    private static readonly HashSet<string> HrefElements =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "a",
            "link",
            "area",
            "base"
        };

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

        if (!IsGetAttributeHref(invocation))
        {
            return;
        }

        var receiverSelector = FindQuerySelectorLiteral(invocation, context.SemanticModel, context.CancellationToken);
        if (receiverSelector is null)
        {
            return;
        }

        var elementType = ExtractElementType(receiverSelector);
        if (elementType is null)
        {
            return;
        }

        if (!HrefElements.Contains(elementType))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Rule,
                invocation.GetLocation(),
                elementType));
        }
    }

    private static bool IsGetAttributeHref(InvocationExpressionSyntax invocation)
    {
        var methodName = invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
            MemberBindingExpressionSyntax memberBinding => memberBinding.Name.Identifier.Text,
            _ => null
        };

        if (!string.Equals(methodName, "GetAttribute", StringComparison.Ordinal))
        {
            return false;
        }

        if (invocation.ArgumentList.Arguments.Count != 1)
        {
            return false;
        }

        var arg = invocation.ArgumentList.Arguments[0].Expression;
        return arg is LiteralExpressionSyntax literal
               && string.Equals(literal.Token.ValueText, "href", StringComparison.Ordinal);
    }

    private static string? FindQuerySelectorLiteral(
        InvocationExpressionSyntax getAttributeCall,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        ExpressionSyntax? receiver = null;

        switch (getAttributeCall.Expression)
        {
            case MemberAccessExpressionSyntax memberAccess:
                receiver = memberAccess.Expression;
                break;
            case MemberBindingExpressionSyntax:
                if (getAttributeCall.Parent is ConditionalAccessExpressionSyntax condAccess)
                {
                    receiver = condAccess.Expression;
                }

                break;
            default:
                return null;
        }

        if (receiver is null)
        {
            return null;
        }

        if (receiver is not IdentifierNameSyntax identifier)
        {
            return null;
        }

        var symbol = model.GetSymbolInfo(identifier, cancellationToken).Symbol;
        if (symbol is not ILocalSymbol local)
        {
            return null;
        }

        foreach (var syntaxRef in local.DeclaringSyntaxReferences)
        {
            var declNode = syntaxRef.GetSyntax(cancellationToken);
            if (declNode is VariableDeclaratorSyntax declarator
                && declarator.Initializer?.Value is InvocationExpressionSyntax initInvocation)
            {
                return ExtractSelectorFromInvocation(initInvocation);
            }

            if (declNode is VariableDeclaratorSyntax declarator2
                && declarator2.Initializer?.Value is ConditionalAccessExpressionSyntax condAccess
                && condAccess.WhenNotNull is InvocationExpressionSyntax condInvocation)
            {
                return ExtractSelectorFromInvocation(condInvocation);
            }
        }

        return null;
    }

    private static string? ExtractSelectorFromInvocation(InvocationExpressionSyntax invocation)
    {
        var methodName = invocation.Expression switch
        {
            MemberAccessExpressionSyntax ma => ma.Name.Identifier.Text,
            MemberBindingExpressionSyntax mb => mb.Name.Identifier.Text,
            _ => null
        };

        if (methodName is null
            || (!string.Equals(methodName, "QuerySelector", StringComparison.Ordinal)
                && !string.Equals(methodName, "QuerySelectorAll", StringComparison.Ordinal)))
        {
            return null;
        }

        if (invocation.ArgumentList.Arguments.Count != 1)
        {
            return null;
        }

        var arg = invocation.ArgumentList.Arguments[0].Expression;
        return arg is LiteralExpressionSyntax literal ? literal.Token.ValueText : null;
    }

    private static string? ExtractElementType(string selector)
    {
        if (string.IsNullOrEmpty(selector))
        {
            return null;
        }

        var end = 0;
        while (end < selector.Length && IsElementNameChar(selector[end]))
        {
            end++;
        }

        return end > 0 ? selector.Substring(0, end) : null;
    }

    private static bool IsElementNameChar(char c)
    {
        return c is >= 'a' and <= 'z'
            or >= 'A' and <= 'Z'
            or >= '0' and <= '9'
            or '-';
    }
}

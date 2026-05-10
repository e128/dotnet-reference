using System;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class GeneratedRegexAnalyzer : DiagnosticAnalyzer
{
    internal const string TimeoutDiagnosticId = "E128011";
    internal const string CompiledDiagnosticId = "E128012";
    internal const string OverlappingQuantifierDiagnosticId = "E128013";
    internal const string NestedQuantifierDiagnosticId = "E128014";
    internal const string MatchTimeoutParameterName = "matchTimeoutMilliseconds";

    private static readonly DiagnosticDescriptor TimeoutRule = new(
        TimeoutDiagnosticId,
        "[GeneratedRegex] attribute is missing 'matchTimeoutMilliseconds'",
        "[GeneratedRegex] attribute is missing 'matchTimeoutMilliseconds' — add a timeout to prevent catastrophic backtracking",
        "Reliability",
        DiagnosticSeverity.Warning,
        true);

    private static readonly DiagnosticDescriptor CompiledRule = new(
        CompiledDiagnosticId,
        "RegexOptions.Compiled is redundant in [GeneratedRegex]",
        "RegexOptions.Compiled is ignored by the source generator — remove it to avoid confusion",
        "Reliability",
        DiagnosticSeverity.Warning,
        true);

    private static readonly DiagnosticDescriptor OverlappingQuantifierRule = new(
        OverlappingQuantifierDiagnosticId,
        "[GeneratedRegex] pattern has overlapping quantifiers that risk catastrophic backtracking",
        "[GeneratedRegex] pattern contains '\\s*' or '\\s+' adjacent to a quantifier with overlapping character set (e.g., '.*', '.+') — this causes exponential backtracking",
        "Reliability",
        DiagnosticSeverity.Warning,
        true);

    private static readonly DiagnosticDescriptor NestedQuantifierRule = new(
        NestedQuantifierDiagnosticId,
        "[GeneratedRegex] pattern has nested quantifiers that cause exponential backtracking",
        "[GeneratedRegex] pattern contains a quantified group with an inner quantifier (e.g., '(.+)+', '(\\w+)+', '(a*)*') — this causes exponential backtracking",
        "Reliability",
        DiagnosticSeverity.Warning,
        true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [TimeoutRule, CompiledRule, OverlappingQuantifierRule, NestedQuantifierRule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.Attribute);
    }

    private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        var attribute = (AttributeSyntax)context.Node;

        var name = attribute.Name switch
        {
            IdentifierNameSyntax id => id.Identifier.ValueText,
            QualifiedNameSyntax q => q.Right.Identifier.ValueText,
            _ => null
        };

        if (!string.Equals(name, "GeneratedRegex", StringComparison.Ordinal)
            && !string.Equals(name, "GeneratedRegexAttribute", StringComparison.Ordinal))
        {
            return;
        }

        var symbolInfo = context.SemanticModel.GetSymbolInfo(attribute, context.CancellationToken);
        if (symbolInfo.Symbol is not IMethodSymbol constructor)
        {
            return;
        }

        if (!IsGeneratedRegexAttribute(constructor.ContainingType))
        {
            return;
        }

        if (!HasTimeoutArgument(attribute, constructor))
        {
            context.ReportDiagnostic(Diagnostic.Create(TimeoutRule, attribute.GetLocation()));
        }

        if (HasCompiledOption(attribute, context.SemanticModel, context.CancellationToken))
        {
            context.ReportDiagnostic(Diagnostic.Create(CompiledRule, attribute.GetLocation()));
        }

        var patternLiteral = GetPatternLiteral(attribute);
        if (patternLiteral is not null)
        {
            if (GeneratedRegexHelpers.HasOverlappingQuantifiers(patternLiteral))
            {
                context.ReportDiagnostic(Diagnostic.Create(OverlappingQuantifierRule, attribute.GetLocation()));
            }

            if (GeneratedRegexHelpers.HasNestedQuantifier(patternLiteral))
            {
                context.ReportDiagnostic(Diagnostic.Create(NestedQuantifierRule, attribute.GetLocation()));
            }
        }
    }

    private static bool HasTimeoutArgument(AttributeSyntax attribute, IMethodSymbol constructor)
    {
        if (attribute.ArgumentList is null)
        {
            return false;
        }

        var arguments = attribute.ArgumentList.Arguments;

        foreach (var arg in arguments)
        {
            if (IsNamedTimeoutArgument(arg))
            {
                return true;
            }
        }

        var positionalCount = CountPositionalArguments(arguments);
        var parameters = constructor.Parameters;
        for (var i = 0; i < parameters.Length && i < positionalCount; i++)
        {
            if (string.Equals(parameters[i].Name, MatchTimeoutParameterName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsNamedTimeoutArgument(AttributeArgumentSyntax arg)
    {
        return (arg.NameColon is { } nameColon
                && string.Equals(
                    nameColon.Name.Identifier.ValueText,
                    MatchTimeoutParameterName,
                    StringComparison.Ordinal))
               || (arg.NameEquals is { } nameEquals
                   && string.Equals(
                       nameEquals.Name.Identifier.ValueText,
                       MatchTimeoutParameterName,
                       StringComparison.Ordinal));
    }

    private static int CountPositionalArguments(
        SeparatedSyntaxList<AttributeArgumentSyntax> arguments)
    {
        var count = 0;
        foreach (var arg in arguments)
        {
            if (arg.NameColon is null && arg.NameEquals is null)
            {
                count++;
            }
        }

        return count;
    }

    private static bool HasCompiledOption(
        AttributeSyntax attribute,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        if (attribute.ArgumentList is null)
        {
            return false;
        }

        ExpressionSyntax? optionsExpression = null;
        var positionalIndex = 0;

        foreach (var arg in attribute.ArgumentList.Arguments)
        {
            if (arg.NameColon is { } nameColon
                && string.Equals(nameColon.Name.Identifier.ValueText, "options", StringComparison.Ordinal))
            {
                optionsExpression = arg.Expression;
                break;
            }

            if (arg.NameColon is null && arg.NameEquals is null)
            {
                if (positionalIndex == 1)
                {
                    optionsExpression = arg.Expression;
                    break;
                }

                positionalIndex++;
            }
        }

        return optionsExpression is not null && ExpressionContainsCompiled(optionsExpression, semanticModel, cancellationToken);
    }

    private static bool ExpressionContainsCompiled(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        if (expression is BinaryExpressionSyntax binary
            && binary.IsKind(SyntaxKind.BitwiseOrExpression))
        {
            return ExpressionContainsCompiled(binary.Left, semanticModel, cancellationToken)
                   || ExpressionContainsCompiled(binary.Right, semanticModel, cancellationToken);
        }

        if (expression is MemberAccessExpressionSyntax memberAccess
            && string.Equals(memberAccess.Name.Identifier.ValueText, "Compiled", StringComparison.Ordinal))
        {
            var symbol = semanticModel.GetSymbolInfo(memberAccess, cancellationToken).Symbol;
            if (symbol is IFieldSymbol { ContainingType: { } containingType }
                && string.Equals(containingType.Name, "RegexOptions", StringComparison.Ordinal))
            {
                var ns = containingType.ContainingNamespace;
                return ns is { Name: "RegularExpressions" }
                       && ns.ContainingNamespace is { Name: "Text" }
                       && ns.ContainingNamespace.ContainingNamespace is { Name: "System" }
                       && ns.ContainingNamespace.ContainingNamespace.ContainingNamespace?.IsGlobalNamespace == true;
            }
        }

        return false;
    }

    private static bool IsGeneratedRegexAttribute(INamedTypeSymbol type)
    {
        if (!string.Equals(type.Name, "GeneratedRegexAttribute", StringComparison.Ordinal))
        {
            return false;
        }

        var ns = type.ContainingNamespace;
        return ns is { Name: "RegularExpressions" }
               && ns.ContainingNamespace is { Name: "Text" }
               && ns.ContainingNamespace.ContainingNamespace is { Name: "System" }
               && ns.ContainingNamespace.ContainingNamespace.ContainingNamespace?.IsGlobalNamespace == true;
    }

    private static string? GetPatternLiteral(AttributeSyntax attribute)
    {
        if (attribute.ArgumentList is null)
        {
            return null;
        }

        var arguments = attribute.ArgumentList.Arguments;
        if (!arguments.Any())
        {
            return null;
        }

        var firstArg = arguments[0];
        return firstArg.NameColon is not null || firstArg.NameEquals is not null
            ? null
            : firstArg.Expression switch
            {
                LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression) =>
                    literal.Token.ValueText,
                _ => null
            };
    }
}

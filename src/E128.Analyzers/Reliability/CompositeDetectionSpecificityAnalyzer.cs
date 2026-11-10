using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CompositeDetectionSpecificityAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128079";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "CompositeDetection with single generic ID selector lacks specificity",
        "CompositeDetection has a single ResourceDetection branch with generic ID selector '{0}' -- add domain or additional detection signals to avoid false positives",
        "E128-Reliability",
        DiagnosticSeverity.Warning,
        true,
        "A CompositeDetection with only one ResourceDetection branch using a generic CSS ID selector " +
        "(e.g., #content, #main) can match unrelated sites. Add a DomainDetection, MetaTagDetection, " +
        "or additional ResourceDetection branch to increase specificity.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeImplicitObjectCreation, SyntaxKind.ImplicitObjectCreationExpression);
    }

    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
    {
        var creation = (ObjectCreationExpressionSyntax)context.Node;

        if (!IsCompositeDetectionType(creation.Type))
        {
            return;
        }

        CheckCollectionArgument(context, creation.ArgumentList);
    }

    private static void AnalyzeImplicitObjectCreation(SyntaxNodeAnalysisContext context)
    {
        var creation = (ImplicitObjectCreationExpressionSyntax)context.Node;
        var typeInfo = context.SemanticModel.GetTypeInfo(creation, context.CancellationToken);
        if (typeInfo.Type is null
            || !string.Equals(typeInfo.Type.Name, "CompositeDetection", StringComparison.Ordinal))
        {
            return;
        }

        CheckCollectionArgument(context, creation.ArgumentList);
    }

    private static void CheckCollectionArgument(
        SyntaxNodeAnalysisContext context,
        ArgumentListSyntax? argumentList)
    {
        var elementExpression = GetSingleElementExpression(argumentList);

        if (elementExpression is not ObjectCreationExpressionSyntax innerCreation)
        {
            return;
        }

        if (!IsResourceDetectionType(innerCreation.Type))
        {
            return;
        }

        var selectorValue = GetSingleStringArgument(innerCreation.ArgumentList);
        if (selectorValue is null)
        {
            return;
        }

        if (!IsGenericIdSelector(selectorValue))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            context.Node.GetLocation(),
            selectorValue));
    }

    private static ExpressionSyntax? GetSingleElementExpression(ArgumentListSyntax? argumentList)
    {
        if (argumentList is null || argumentList.Arguments.Count is not 1)
        {
            return null;
        }

        if (argumentList.Arguments[0].Expression is not CollectionExpressionSyntax { Elements.Count: 1 } collection)
        {
            return null;
        }

        return collection.Elements[0] is ExpressionElementSyntax exprElement ? exprElement.Expression : null;
    }

    private static bool IsCompositeDetectionType(TypeSyntax? type)
    {
        return type switch
        {
            QualifiedNameSyntax qualified =>
                string.Equals(qualified.Right.Identifier.Text, "CompositeDetection", StringComparison.Ordinal),
            IdentifierNameSyntax identifier =>
                string.Equals(identifier.Identifier.Text, "CompositeDetection", StringComparison.Ordinal),
            _ => false
        };
    }

    private static bool IsResourceDetectionType(TypeSyntax? type)
    {
        return type switch
        {
            QualifiedNameSyntax qualified =>
                string.Equals(qualified.Right.Identifier.Text, "ResourceDetection", StringComparison.Ordinal),
            IdentifierNameSyntax identifier =>
                string.Equals(identifier.Identifier.Text, "ResourceDetection", StringComparison.Ordinal),
            _ => false
        };
    }

    private static string? GetSingleStringArgument(ArgumentListSyntax? argumentList)
    {
        if (argumentList is null || argumentList.Arguments.Count is not 1)
        {
            return null;
        }

        var arg = argumentList.Arguments[0].Expression;
        return arg is LiteralExpressionSyntax literal
               && literal.IsKind(SyntaxKind.StringLiteralExpression)
            ? literal.Token.ValueText
            : null;
    }

    private static bool IsGenericIdSelector(string selector)
    {
        if (selector.Length < 2 || selector[0] != '#')
        {
            return false;
        }

        for (var i = 1; i < selector.Length; i++)
        {
            var c = selector[i];
            if (!IsSelectorChar(c))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSelectorChar(char c)
    {
        return c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '-' or '_';
    }
}

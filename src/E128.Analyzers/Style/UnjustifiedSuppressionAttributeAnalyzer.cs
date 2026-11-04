using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Style;

/// <summary>
///     E128173: Flags <c lang="csharp">[SuppressMessage]</c> and <c lang="csharp">[ExcludeFromCodeCoverage]</c> with no
///     <c lang="csharp">Justification</c>. The reason lives in the attribute so a later reader finds it.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnjustifiedSuppressionAttributeAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128173";

    private static readonly string[] CheckedAttributes =
    [
        "System.Diagnostics.CodeAnalysis.SuppressMessageAttribute",
        "System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute"
    ];

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Justify every suppression attribute",
        "[{0}] has no Justification: state why the suppression is safe",
        "E128-Style",
        DiagnosticSeverity.Warning,
        true,
        "A suppression or coverage exclusion with no reason cannot be reviewed or removed later. Set the Justification argument.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeAttribute, SyntaxKind.Attribute);
    }

    private static void AnalyzeAttribute(SyntaxNodeAnalysisContext context)
    {
        var attribute = (AttributeSyntax)context.Node;

        if (context.SemanticModel.GetSymbolInfo(attribute, context.CancellationToken).Symbol is not IMethodSymbol constructor
            || Array.IndexOf(CheckedAttributes, constructor.ContainingType.ToDisplayString()) < 0)
        {
            return;
        }

        if (!HasJustification(attribute, context))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, attribute.GetLocation(), constructor.ContainingType.Name.Replace("Attribute", string.Empty)));
        }
    }

    private static bool HasJustification(AttributeSyntax attribute, SyntaxNodeAnalysisContext context)
    {
        if (attribute.ArgumentList is null)
        {
            return false;
        }

        foreach (var argument in attribute.ArgumentList.Arguments)
        {
            if (argument.NameEquals is { } nameEquals
                && string.Equals(nameEquals.Name.Identifier.ValueText, "Justification", StringComparison.Ordinal))
            {
                var constant = context.SemanticModel.GetConstantValue(argument.Expression, context.CancellationToken);

                return !constant.HasValue || constant.Value is not string text || !string.IsNullOrWhiteSpace(text);
            }
        }

        return false;
    }
}

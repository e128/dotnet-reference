using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Style;

/// <summary>
///     E128169: Flags <c lang="csharp">[Obsolete]</c> with no message. A caller needs the replacement named in the warning.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ObsoleteWithoutMessageAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128169";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Explain every Obsolete attribute",
        "[Obsolete] has no message: name the replacement so a caller knows what to use",
        "E128-Style",
        DiagnosticSeverity.Warning,
        true,
        "An Obsolete attribute with no message tells a caller to stop but not where to go. Pass a message that names the replacement.");

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
            || !string.Equals(constructor.ContainingType.ToDisplayString(), "System.ObsoleteAttribute", StringComparison.Ordinal))
        {
            return;
        }

        if (!HasMessage(attribute, context))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, attribute.GetLocation()));
        }
    }

    private static bool HasMessage(AttributeSyntax attribute, SyntaxNodeAnalysisContext context)
    {
        if (attribute.ArgumentList is null)
        {
            return false;
        }

        foreach (var argument in attribute.ArgumentList.Arguments)
        {
            if (argument.NameEquals is not null)
            {
                continue;
            }

            var constant = context.SemanticModel.GetConstantValue(argument.Expression, context.CancellationToken);

            return !constant.HasValue || constant.Value is not string text || !string.IsNullOrWhiteSpace(text);
        }

        return false;
    }
}

using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AggressiveOptimizationAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128116";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Do not use MethodImplOptions.AggressiveOptimization",
        "AggressiveOptimization disables tiered compilation for the method and usually lowers steady-state throughput",
        "Performance",
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
        context.RegisterSyntaxNodeAction(AnalyzeAttribute, SyntaxKind.Attribute);
    }

    private static void AnalyzeAttribute(SyntaxNodeAnalysisContext context)
    {
        var attribute = (AttributeSyntax)context.Node;

        if (attribute.ArgumentList is null)
        {
            return;
        }

        // Resolve the constructor, so `MethodImpl`, `MethodImplAttribute` and a qualified
        // name all match the same type.
        if (context.SemanticModel.GetSymbolInfo(attribute, context.CancellationToken).Symbol
            is not IMethodSymbol { ContainingType.Name: "MethodImplAttribute" })
        {
            return;
        }

        if (!ContainsAggressiveOptimization(attribute.ArgumentList))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, attribute.GetLocation()));
    }

    internal static bool ContainsAggressiveOptimization(AttributeArgumentListSyntax argumentList)
    {
        foreach (var argument in argumentList.Arguments)
        {
            foreach (var identifier in argument.Expression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
            {
                if (string.Equals(identifier.Identifier.ValueText, "AggressiveOptimization", StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }
}

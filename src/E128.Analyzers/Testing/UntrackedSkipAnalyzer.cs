using System;
using System.Collections.Immutable;
using E128.Analyzers.Style;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Testing;

/// <summary>
///     E128168: Flags an xUnit <c lang="csharp">[Fact]</c> or <c lang="csharp">[Theory]</c> whose <c lang="csharp">Skip</c> reason carries no work-item
///     reference. A skipped test with no ticket stays skipped.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UntrackedSkipAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128168";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Track every skipped test",
        "Skipped test has no work-item reference in its reason: add one that matches e128_tracked_ref_pattern",
        "E128-Testing",
        DiagnosticSeverity.Warning,
        true,
        "A skipped test with no work-item reference has no owner and no close date. Put a reference such as AB#123 in the Skip reason, or set e128_tracked_ref_pattern to your tracker format.");

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

        if (attribute.ArgumentList is null
            || !XunitTestHelper.IsFactOrTheoryAttribute(attribute, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        foreach (var argument in attribute.ArgumentList.Arguments)
        {
            if (argument.NameEquals is not { } nameEquals
                || !string.Equals(nameEquals.Name.Identifier.ValueText, "Skip", StringComparison.Ordinal))
            {
                continue;
            }

            var constant = context.SemanticModel.GetConstantValue(argument.Expression, context.CancellationToken);

            if (!constant.HasValue || constant.Value is not string reason)
            {
                return;
            }

            var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(attribute.SyntaxTree);

            if (!TrackerReference.IsTracked(TrackerReference.GetPattern(options), reason))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, argument.GetLocation()));
            }

            return;
        }
    }
}

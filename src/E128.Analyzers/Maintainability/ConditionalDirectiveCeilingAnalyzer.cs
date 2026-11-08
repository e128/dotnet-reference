using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Maintainability;

/// <summary>
///     E128201: Reports a file that holds more conditional compilation directives than the
///     configured ceiling. Each directive multiplies the set of build variants, so a small ceiling
///     keeps the tested matrix bounded. The ceiling is the
///     <c lang="ini">e128_max_conditional_directives</c> option.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ConditionalDirectiveCeilingAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128201";

    internal const string CeilingOptionKey = "e128_max_conditional_directives";

    private const int DefaultCeiling = 4;

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Conditional directives past the ceiling",
        "This file holds {0} conditional compilation directives, past the configured ceiling of {1}. Reduce the build variants or raise e128_max_conditional_directives.",
        "E128-Maintainability",
        DiagnosticSeverity.Info,
        true,
        "Each conditional directive multiplies the set of build variants. A small ceiling keeps the " +
        "tested matrix bounded.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxTreeAction(AnalyzeTree);
    }

    private static void AnalyzeTree(SyntaxTreeAnalysisContext context)
    {
        var root = context.Tree.GetRoot(context.CancellationToken);
        var directives = new List<Location>();

        foreach (var trivia in root.DescendantTrivia())
        {
            if (trivia.IsKind(SyntaxKind.IfDirectiveTrivia))
            {
                directives.Add(trivia.GetLocation());
            }
        }

        var ceiling = GetCeiling(context.Tree, context.Options.AnalyzerConfigOptionsProvider);

        if (directives.Count <= ceiling)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, directives[ceiling], directives.Count, ceiling));
    }

    private static int GetCeiling(SyntaxTree tree, AnalyzerConfigOptionsProvider provider)
    {
        return OptionReader.ReadInt(
            provider.GetOptions(tree),
            CeilingOptionKey,
            DefaultCeiling,
            minimum: 0);
    }
}

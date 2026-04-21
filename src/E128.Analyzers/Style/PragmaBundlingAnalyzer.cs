using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Style;

/// <summary>
///     E128065: Detects <c>#pragma warning disable</c> directives that list more than one
///     diagnostic ID. Each ID requires independent justification; bundling hides weak
///     reasoning and makes suppression audits harder. Companion to E128055.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PragmaBundlingAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128065";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Bundled pragma warning disable",
        "#pragma warning disable lists {0} IDs; each ID requires independent justification — use one pragma per ID",
        "Style",
        DiagnosticSeverity.Warning,
        true,
        "Bundling multiple diagnostic IDs on one #pragma warning disable hides weak suppression reasoning. " +
        "Split into one pragma per ID so each can carry its own justification comment. Companion to E128055.");

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

        foreach (var trivia in root.DescendantTrivia())
        {
            if (!trivia.IsKind(SyntaxKind.PragmaWarningDirectiveTrivia))
            {
                continue;
            }

            var pragma = (PragmaWarningDirectiveTriviaSyntax)trivia.GetStructure()!;

            if (!pragma.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword))
            {
                continue;
            }

            if (pragma.ErrorCodes.Count <= 1)
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(Rule, pragma.GetLocation(), pragma.ErrorCodes.Count));
        }
    }
}

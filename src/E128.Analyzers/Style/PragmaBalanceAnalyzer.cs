using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Style;

/// <summary>
///     E128055: Detects <c lang="csharp">#pragma warning disable</c> directives that are never matched
///     by a corresponding <c lang="csharp">#pragma warning restore</c>, leaving suppressions unbounded.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PragmaBalanceAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128055";
    internal const string DiagnosticIdKey = "SuppressedId";

    // A bare #pragma warning disable has no error code. The key stands for "every warning".
    internal const string AllCodesKey = "*";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Unbalanced pragma warning disable",
        "Unbalanced pragma: '{0}' is disabled but never restored",
        "Style",
        DiagnosticSeverity.Warning,
        true,
        "Every #pragma warning disable should have a matching #pragma warning restore to limit the scope of the suppression.");

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
        var openSuppressions = CollectOpenSuppressions(root);
        ReportUnbalanced(context, openSuppressions);
    }

    // Directives apply in document order. A restore clears only a disable that comes before it, so a
    // file that restores first and disables later leaves the suppression open.
    private static Dictionary<string, Location> CollectOpenSuppressions(SyntaxNode root)
    {
        var open = new Dictionary<string, Location>(StringComparer.Ordinal);

        foreach (var trivia in root.DescendantTrivia())
        {
            if (!trivia.IsKind(SyntaxKind.PragmaWarningDirectiveTrivia))
            {
                continue;
            }

            ApplyPragma(trivia.GetStructure() as PragmaWarningDirectiveTriviaSyntax, open);
        }

        return open;
    }

    private static void ApplyPragma(
        PragmaWarningDirectiveTriviaSyntax? pragma,
        Dictionary<string, Location> open)
    {
        if (pragma is null)
        {
            return;
        }

        var isDisable = pragma.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword);

        if (!pragma.ErrorCodes.Any())
        {
            if (!isDisable)
            {
                // A bare #pragma warning restore restores every open suppression.
                open.Clear();
                return;
            }

            // A bare disable turns off every warning, so it is unbalanced until a bare restore.
            if (!open.ContainsKey(AllCodesKey))
            {
                open[AllCodesKey] = pragma.GetLocation();
            }

            return;
        }

        foreach (var code in pragma.ErrorCodes)
        {
            var id = code.ToString().Trim();
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }

            if (!isDisable)
            {
                open.Remove(id);
            }
            else if (!open.ContainsKey(id))
            {
                open[id] = pragma.GetLocation();
            }
        }
    }

    private static void ReportUnbalanced(
        SyntaxTreeAnalysisContext context,
        Dictionary<string, Location> openSuppressions)
    {
        foreach (var kvp in openSuppressions)
        {
            var properties = ImmutableDictionary<string, string?>.Empty.Add(DiagnosticIdKey, kvp.Key);
            context.ReportDiagnostic(Diagnostic.Create(Rule, kvp.Value, properties, kvp.Key));
        }
    }
}

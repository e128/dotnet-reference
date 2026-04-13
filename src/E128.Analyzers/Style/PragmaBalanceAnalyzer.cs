using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Style;

/// <summary>
/// E128055: Detects <c>#pragma warning disable</c> directives that are never matched
/// by a corresponding <c>#pragma warning restore</c>, leaving suppressions unbounded.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PragmaBalanceAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128055";
    internal const string DiagnosticIdKey = "SuppressedId";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Unbalanced pragma warning disable",
        messageFormat: "Unbalanced pragma: '{0}' is disabled but never restored",
        category: "Style",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Every #pragma warning disable should have a matching #pragma warning restore to limit the scope of the suppression.");

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
        CollectPragmaIds(root, out var disabled, out var restored);
        ReportUnbalanced(context, disabled, restored);
    }

    private static void CollectPragmaIds(
        SyntaxNode root,
        out Dictionary<string, Location> disabled,
        out HashSet<string> restored)
    {
        disabled = new Dictionary<string, Location>(StringComparer.Ordinal);
        restored = new HashSet<string>(StringComparer.Ordinal);

        foreach (var trivia in root.DescendantTrivia())
        {
            if (!trivia.IsKind(SyntaxKind.PragmaWarningDirectiveTrivia))
            {
                continue;
            }

            var pragma = (PragmaWarningDirectiveTriviaSyntax)trivia.GetStructure()!;
            var isDisable = pragma.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword);
            var isRestore = pragma.DisableOrRestoreKeyword.IsKind(SyntaxKind.RestoreKeyword);

            if (!pragma.ErrorCodes.Any())
            {
                // Bare #pragma warning restore restores all previously disabled IDs.
                if (isRestore)
                {
                    foreach (var disabledId in disabled.Keys)
                    {
                        restored.Add(disabledId);
                    }
                }

                continue;
            }

            ProcessPragmaCodes(pragma.ErrorCodes, isDisable, isRestore, disabled, restored, pragma.GetLocation());
        }
    }

    private static void ProcessPragmaCodes(
        SeparatedSyntaxList<ExpressionSyntax> codes,
        bool isDisable,
        bool isRestore,
        Dictionary<string, Location> disabled,
        HashSet<string> restored,
        Location pragmaLocation)
    {
        foreach (var code in codes)
        {
            var id = code.ToString().Trim();
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }

            if (isDisable && !disabled.ContainsKey(id))
            {
                disabled[id] = pragmaLocation;
            }
            else if (isRestore)
            {
                restored.Add(id);
            }
        }
    }

    private static void ReportUnbalanced(
        SyntaxTreeAnalysisContext context,
        Dictionary<string, Location> disabled,
        HashSet<string> restored)
    {
        foreach (var kvp in disabled)
        {
            if (restored.Contains(kvp.Key))
            {
                continue;
            }

            var properties = ImmutableDictionary<string, string?>.Empty.Add(DiagnosticIdKey, kvp.Key);
            context.ReportDiagnostic(Diagnostic.Create(Rule, kvp.Value, properties, kvp.Key));
        }
    }
}

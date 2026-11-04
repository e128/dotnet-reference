using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Style;

/// <summary>
///     E128167: Flags a deferred-work marker comment that carries no work-item reference.
///     A deferral with no ticket is never closed.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UntrackedTodoAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128167";

    private static readonly string[] Markers = ["TODO", "FIXME", "HACK"];

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Track every deferred-work comment",
        "'{0}' comment has no work-item reference: add one that matches e128_tracked_ref_pattern, or do the work now",
        "E128-Style",
        DiagnosticSeverity.Warning,
        true,
        "A TODO, FIXME, or HACK comment with no work-item reference has no owner and no close date. Add a reference such as AB#123, or set e128_tracked_ref_pattern to your tracker format.");

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
        var pattern = TrackerReference.GetPattern(context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Tree));

        foreach (var trivia in root.DescendantTrivia())
        {
            if (!trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) && !trivia.IsKind(SyntaxKind.MultiLineCommentTrivia))
            {
                continue;
            }

            var text = trivia.ToString();
            var marker = FindMarker(text);

            if (marker is not null && !TrackerReference.IsTracked(pattern, text))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, trivia.GetLocation(), marker));
            }
        }
    }

    private static string? FindMarker(string text)
    {
        // A marker counts only at the start of a comment line, so prose such as
        // "this avoids a hack" does not report.
        foreach (var line in text.Split('\n'))
        {
            var body = line.TrimStart(' ', '\t', '\r', '/', '*');

            foreach (var marker in Markers)
            {
                if (!body.StartsWith(marker, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var end = marker.Length;

                if (end == body.Length || !char.IsLetterOrDigit(body[end]))
                {
                    return marker;
                }
            }
        }

        return null;
    }
}

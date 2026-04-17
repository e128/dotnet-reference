using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Style;

/// <summary>
///     E128047: Flags <c>#pragma warning disable</c> directives that lack a justification comment.
///     Every suppression must explain why it is necessary.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SuppressionAuditAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128047";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "#pragma warning disable without justification comment",
        "Add a justification comment explaining why this suppression is necessary",
        "Style",
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
        context.RegisterSyntaxTreeAction(AnalyzeSyntaxTree);
    }

    private static void AnalyzeSyntaxTree(SyntaxTreeAnalysisContext context)
    {
        var root = context.Tree.GetRoot(context.CancellationToken);
        AnalyzeTrivia(root, context);
    }

    private static void AnalyzeTrivia(SyntaxNode root, SyntaxTreeAnalysisContext context)
    {
        foreach (var trivia in root.DescendantTrivia())
        {
            if (!trivia.IsKind(SyntaxKind.PragmaWarningDirectiveTrivia))
            {
                continue;
            }

            var directive = trivia.GetStructure();
            if (directive is null)
            {
                continue;
            }

            // Only check #pragma warning disable — not restore.
            if (!directive.ToString().Contains("disable", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (HasJustificationComment(trivia))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(Rule, directive.GetLocation()));
        }
    }

    // Returns true if the pragma has a trailing comment on the same line or a comment on the immediately
    // preceding line. This covers both styles:
    //   #pragma warning disable CS1234 // reason
    //   // reason
    //   #pragma warning disable CS1234
    private static bool HasJustificationComment(SyntaxTrivia pragmaTrivia)
    {
        // Check trailing trivia on the directive structure for a comment.
        var directive = pragmaTrivia.GetStructure();
        if (directive is not null)
        {
            foreach (var token in directive.DescendantTokens())
            {
                foreach (var trailing in token.TrailingTrivia)
                {
                    if (trailing.IsKind(SyntaxKind.SingleLineCommentTrivia)
                        || trailing.IsKind(SyntaxKind.MultiLineCommentTrivia))
                    {
                        return true;
                    }
                }
            }
        }

        // Check the preceding trivia for a comment on the line immediately before.
        var token2 = pragmaTrivia.Token;
        var allTrivia = token2.LeadingTrivia;
        var pragmaIndex = allTrivia.IndexOf(pragmaTrivia);

        for (var i = pragmaIndex - 1; i >= 0; i--)
        {
            var preceding = allTrivia[i];

            if (preceding.IsKind(SyntaxKind.SingleLineCommentTrivia)
                || preceding.IsKind(SyntaxKind.MultiLineCommentTrivia))
            {
                return true;
            }

            // Stop at a blank line (two consecutive end-of-line trivia).
            if (preceding.IsKind(SyntaxKind.EndOfLineTrivia)
                && i > 0 && allTrivia[i - 1].IsKind(SyntaxKind.EndOfLineTrivia))
            {
                return false;
            }

            // Stop at non-whitespace, non-comment trivia.
            if (!preceding.IsKind(SyntaxKind.WhitespaceTrivia)
                && !preceding.IsKind(SyntaxKind.EndOfLineTrivia))
            {
                return false;
            }
        }

        return false;
    }
}

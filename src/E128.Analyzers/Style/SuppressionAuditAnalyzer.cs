using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Style;

/// <summary>
///     E128047: Flags <c lang="csharp">#pragma warning disable</c> directives that lack a justification comment.
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
        "E128-Style",
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
        foreach (var (trivia, directive) in PragmaWarningDirectives.Warning(root))
        {
            if (!PragmaWarningDirectives.IsDisable(directive))
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

    private static bool HasJustificationComment(SyntaxTrivia pragmaTrivia)
    {
        return HasTrailingComment(pragmaTrivia.GetStructure()) || HasPrecedingComment(pragmaTrivia);
    }

    private static bool IsComment(SyntaxTrivia trivia)
    {
        return trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
               || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia);
    }

    private static bool HasTrailingComment(SyntaxNode? directive)
    {
        if (directive is null)
        {
            return false;
        }

        foreach (var token in directive.DescendantTokens())
        {
            foreach (var trailing in token.TrailingTrivia)
            {
                if (IsComment(trailing))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasPrecedingComment(SyntaxTrivia pragmaTrivia)
    {
        var allTrivia = pragmaTrivia.Token.LeadingTrivia;
        var pragmaIndex = allTrivia.IndexOf(pragmaTrivia);

        for (var i = pragmaIndex - 1; i >= 0; i--)
        {
            var preceding = allTrivia[i];

            if (IsComment(preceding))
            {
                return true;
            }

            if (preceding.IsKind(SyntaxKind.EndOfLineTrivia)
                && i > 0 && allTrivia[i - 1].IsKind(SyntaxKind.EndOfLineTrivia))
            {
                return false;
            }

            if (!preceding.IsKind(SyntaxKind.WhitespaceTrivia)
                && !preceding.IsKind(SyntaxKind.EndOfLineTrivia))
            {
                return false;
            }
        }

        return false;
    }
}

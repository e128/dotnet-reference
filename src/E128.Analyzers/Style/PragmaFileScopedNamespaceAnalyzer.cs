using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Style;

/// <summary>
///     E128094: Detects a <c lang="csharp">#pragma warning disable</c> directive placed above a
///     file-scoped namespace declaration (<c lang="csharp">namespace X;</c>). IDE0079 removes the
///     directive during format when it sits outside the namespace it was meant to
///     scope, so the suppression silently stops working.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PragmaFileScopedNamespaceAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128094";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Pragma warning disable placed before a file-scoped namespace declaration",
        "#pragma warning disable precedes the file-scoped namespace declaration — move it below the namespace or IDE0079 will remove it during format",
        "Style",
        DiagnosticSeverity.Error,
        true,
        "A #pragma warning disable above a file-scoped namespace declaration sits outside the namespace it was meant to scope. IDE0079 treats it as unnecessary and removes it during format, silently disabling the suppression.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.FileScopedNamespaceDeclaration);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var namespaceDeclaration = (FileScopedNamespaceDeclarationSyntax)context.Node;
        var namespaceToken = namespaceDeclaration.GetFirstToken();

        // Walk backwards from the namespace and stop at the first match. The old code walked
        // every token in the file, including the whole body below the namespace.
        var found = FindDisablePragma(namespaceToken.LeadingTrivia);
        for (var token = namespaceToken.GetPreviousToken(includeZeroWidth: true);
             found is null && token.RawKind != 0;
             token = token.GetPreviousToken(includeZeroWidth: true))
        {
            found = FindDisablePragma(token.TrailingTrivia) ?? FindDisablePragma(token.LeadingTrivia);
        }

        if (found is { } trivia)
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, trivia.GetLocation()));
        }
    }

    private static SyntaxTrivia? FindDisablePragma(SyntaxTriviaList triviaList)
    {
        foreach (var trivia in triviaList)
        {
            if (trivia.IsKind(SyntaxKind.PragmaWarningDirectiveTrivia)
                && trivia.GetStructure() is PragmaWarningDirectiveTriviaSyntax pragma
                && pragma.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword))
            {
                return trivia;
            }
        }

        return null;
    }
}

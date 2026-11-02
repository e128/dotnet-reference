using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Style;

/// <summary>
///     E128024: Detects non-XML-doc <c lang="csharp">//</c> comments immediately preceding method or
///     local function declarations. Use <c lang="csharp">/// &lt;summary&gt;</c> XML doc comments or nothing.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NonXmlDocCommentE128Analyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128024";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Non-XML-doc comment above method declaration",
        "Use /// XML doc comment or remove — // comments above methods are not allowed",
        "Style",
        DiagnosticSeverity.Warning,
        true,
        "Non-XML-doc // comments immediately before method declarations are prohibited. " +
        "Use /// <summary> XML doc comments for documentation, or remove the comment entirely.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            AnalyzeNode,
            SyntaxKind.MethodDeclaration,
            SyntaxKind.LocalFunctionStatement);
    }

    private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        var leadingTrivia = context.Node.GetLeadingTrivia();

        // A comment covered by an open #pragma warning disable justifies the suppression. The
        // fix removes the comment, which would drop that reason and trip IDE0079. Only a
        // disable counts: a restore that ends an earlier pragma block lands in the next
        // member's leading trivia and must not suppress this rule.
        if (leadingTrivia.Any(trivia => trivia.GetStructure() is PragmaWarningDirectiveTriviaSyntax pragma
            && pragma.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword)))
        {
            return;
        }

        foreach (var trivia in leadingTrivia)
        {
            if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
                && !IsSingleLineDocComment(trivia))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Rule,
                    trivia.GetLocation()));
            }
        }
    }

    private static bool IsSingleLineDocComment(SyntaxTrivia trivia)
    {
        var text = trivia.ToString();
        return text.StartsWith("///", StringComparison.Ordinal);
    }
}

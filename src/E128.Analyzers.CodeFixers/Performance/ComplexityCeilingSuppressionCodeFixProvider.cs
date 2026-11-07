using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Performance;

/// <summary>
///     Code fix for E128152: wraps the reported loop, or the reported recursive method, in a
///     <c lang="csharp">#pragma warning disable E128152</c> pair that carries a justification
///     comment. E128047 rejects a suppression without one.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ComplexityCeilingSuppressionCodeFixProvider))]
[Shared]
public sealed class ComplexityCeilingSuppressionCodeFixProvider : E128CodeFixProviderBase
{
    internal const string DisableDirective =
        "#pragma warning disable " + ComplexityCeilingAnalyzer.DiagnosticId +
        " // Justification: state why this site is output-bound";

    internal const string RestoreDirective =
        "#pragma warning restore " + ComplexityCeilingAnalyzer.DiagnosticId;

    public override ImmutableArray<string> FixableDiagnosticIds =>
        [ComplexityCeilingAnalyzer.DiagnosticId];

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
        var anchor = FindAnchor(node);

        if (anchor is null)
        {
            return Task.CompletedTask;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Suppress with a justification comment",
                _ => Task.FromResult(ApplySuppression(context.Document, root, anchor)),
                nameof(ComplexityCeilingSuppressionCodeFixProvider)),
            diagnostic);

        return Task.CompletedTask;
    }

    /// <summary>
    ///     The loop rule reports at a statement and the recursion rule reports at a method
    ///     declaration. A statement that is not one of a block, a switch section, or the
    ///     compilation unit sits in an embedded position, where a directive line reads as part
    ///     of the enclosing construct.
    /// </summary>
    private static SyntaxNode? FindAnchor(SyntaxNode node)
    {
        var statement = node as StatementSyntax ?? node.FirstAncestorOrSelf<StatementSyntax>();

        return statement is not null
            ? statement.Parent is BlockSyntax or SwitchSectionSyntax or GlobalStatementSyntax
                ? statement
                : null
            : node as MethodDeclarationSyntax;
    }

    private static Document ApplySuppression(Document document, SyntaxNode root, SyntaxNode anchor)
    {
        var indent = IndentationOf(anchor);
        var newAnchor = anchor
            .WithLeadingTrivia(LeadingWithDisable(anchor, indent))
            .WithTrailingTrivia(TrailingWithRestore(anchor, indent));

        return document.WithSyntaxRoot(root.ReplaceNode(anchor, newAnchor));
    }

    private static SyntaxTriviaList LeadingWithDisable(SyntaxNode anchor, string indent)
    {
        var leading = anchor.GetLeadingTrivia();
        var lastIndex = leading.Count - 1;
        var directives = Directives(indent, DisableDirective);

        return lastIndex >= 0 && leading[lastIndex].IsKind(SyntaxKind.WhitespaceTrivia)
            ? leading.InsertRange(lastIndex, directives)
            : leading.AddRange(directives);
    }

    private static SyntaxTriviaList TrailingWithRestore(SyntaxNode anchor, string indent)
    {
        var trailing = anchor.GetTrailingTrivia();

        if (!trailing.Any(SyntaxKind.EndOfLineTrivia))
        {
            trailing = trailing.Add(SyntaxFactory.LineFeed);
        }

        return trailing.AddRange(Directives(indent, RestoreDirective));
    }

    /// <summary>
    ///     The indentation of the anchor line, read from the anchor's own leading whitespace.
    ///     A statement at the head of a block carries that whitespace, so the value is the
    ///     indent the surrounding block already uses.
    /// </summary>
    private static string IndentationOf(SyntaxNode anchor)
    {
        var leading = anchor.GetLeadingTrivia();
        var lastIndex = leading.Count - 1;

        return lastIndex >= 0 && leading[lastIndex].IsKind(SyntaxKind.WhitespaceTrivia)
            ? leading[lastIndex].ToString()
            : string.Empty;
    }

    /// <summary>
    ///     Parses the directive line rather than assembling it from
    ///     <c lang="csharp">SyntaxFactory</c> tokens. A parsed directive keeps its indentation
    ///     and its justification comment when the document text is materialized.
    /// </summary>
    private static SyntaxTriviaList Directives(string indent, string directive)
    {
        return SyntaxFactory.ParseLeadingTrivia(indent + directive + "\n");
    }
}

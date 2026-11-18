using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Design;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UnionSwitchDiscardArmCodeFixProvider))]
[Shared]
public sealed class UnionSwitchDiscardArmCodeFixProvider : E128CodeFixProviderBase
{
    // The union analyzer ships in the Roslyn 5.12 assembly, which this Roslyn 5.9
    // build does not reference. Roslyn matches a fixer to a diagnostic by id, so
    // the fixer needs no compile-time reference to the analyzer type.
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ["E128220"];

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        if (root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is not DiscardPatternSyntax pattern
            || pattern.Parent is SwitchExpressionArmSyntax { WhenClause: not null }
                or CasePatternSwitchLabelSyntax { WhenClause: not null })
        {
            // A var arm binds a case value, and a when guard would survive on the null arm,
            // so replacing it with null would break the arm's expression. The rewrite needs
            // the developer.
            return Task.CompletedTask;
        }

        CodeFixRegistration.Register(
            context,
            diagnostic,
            "Replace the discard arm with a null arm",
            nameof(UnionSwitchDiscardArmCodeFixProvider),
            ct => ReplaceDiscardWithNullAsync(context.Document, root, pattern, ct));

        return Task.CompletedTask;
    }

    private static Task<Document> ReplaceDiscardWithNullAsync(
        Document document,
        SyntaxNode root,
        DiscardPatternSyntax pattern,
        CancellationToken cancellationToken)
    {
        var nullPattern = SyntaxFactory.ConstantPattern(
                SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression))
            .WithTriviaFrom(pattern);
        var newRoot = root.ReplaceNode(pattern, nullPattern);

        _ = cancellationToken;
        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }
}

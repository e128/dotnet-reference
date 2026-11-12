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
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [UnionSwitchDiscardArmAnalyzer.DiagnosticId];

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        if (root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is not DiscardPatternSyntax pattern)
        {
            // A var arm binds a case value, so replacing it with null would break the arm's
            // expression. The rewrite needs the developer.
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

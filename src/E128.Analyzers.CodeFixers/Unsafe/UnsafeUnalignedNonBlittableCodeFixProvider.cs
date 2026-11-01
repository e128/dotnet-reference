using System;
using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Unsafe;

/// <summary>
///     Code fix for E128139: a <c lang="csharp">bool</c> read through
///     <c lang="csharp">Unsafe.ReadUnaligned</c> becomes a comparison with zero.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UnsafeUnalignedNonBlittableCodeFixProvider))]
[Shared]
public sealed class UnsafeUnalignedNonBlittableCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [UnsafeUnalignedNonBlittableAnalyzer.DiagnosticId];

    public override FixAllProvider? GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);

        if (node is not InvocationExpressionSyntax
            {
                Expression: MemberAccessExpressionSyntax { Name: GenericNameSyntax generic },
            } invocation
            || !string.Equals(generic.Identifier.ValueText, "ReadUnaligned", StringComparison.Ordinal)
            || generic.TypeArgumentList.Arguments.Count != 1
            || generic.TypeArgumentList.Arguments[0] is not PredefinedTypeSyntax predefinedType
            || !predefinedType.Keyword.IsKind(SyntaxKind.BoolKeyword)
            || invocation.ArgumentList.Arguments.Count != 1)
        {
            return;
        }

        // A comparison is not a valid expression statement, so leave that shape alone.
        if (invocation.Parent is ExpressionStatementSyntax)
        {
            return;
        }

        var argument = invocation.ArgumentList.Arguments[0];

        // ReadUnaligned takes a byref, so the argument must carry a ref, in, or out keyword.
        if (argument.RefKindKeyword.IsKind(SyntaxKind.None))
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Compare the byte with zero",
                _ => Task.FromResult(ApplyFix(context.Document, root, invocation, argument.Expression)),
                nameof(UnsafeUnalignedNonBlittableCodeFixProvider)),
            diagnostic);
    }

    private static Document ApplyFix(
        Document document,
        SyntaxNode root,
        InvocationExpressionSyntax invocation,
        ExpressionSyntax source)
    {
        var comparison = SyntaxFactory.BinaryExpression(
            SyntaxKind.NotEqualsExpression,
            FixSyntax.ParenthesizeIfNeeded(source),
            SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0)));
        var replacement = SyntaxFactory.ParenthesizedExpression(comparison).WithTriviaFrom(invocation);
        return document.WithSyntaxRoot(root.ReplaceNode(invocation, replacement));
    }
}

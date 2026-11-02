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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MemoryMarshalNonBlittableCodeFixProvider))]
[Shared]
public sealed class MemoryMarshalNonBlittableCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [MemoryMarshalNonBlittableAnalyzer.DiagnosticId];

    public override FixAllProvider? GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        if (node is not InvocationExpressionSyntax
            {
                Expression: MemberAccessExpressionSyntax { Name: GenericNameSyntax generic },
            } invocation
            || invocation.ArgumentList.Arguments.Count != 1)
        {
            return Task.CompletedTask;
        }

        if (!string.Equals(generic.Identifier.ValueText, "Read", StringComparison.Ordinal)
            || generic.TypeArgumentList.Arguments.Count != 1
            || !string.Equals(generic.TypeArgumentList.Arguments[0].ToString(), "bool", StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        var source = invocation.ArgumentList.Arguments[0].Expression;

        context.RegisterCodeFix(
            CodeAction.Create(
                "Compare the first byte",
                _ => Task.FromResult(ApplyFix(context.Document, root, invocation, source)),
                nameof(MemoryMarshalNonBlittableCodeFixProvider)),
            diagnostic);

        return Task.CompletedTask;
    }

    private static Document ApplyFix(
        Document document,
        SyntaxNode root,
        InvocationExpressionSyntax invocation,
        ExpressionSyntax source)
    {
        var comparison = SyntaxFactory.BinaryExpression(
            SyntaxKind.NotEqualsExpression,
            FixSyntax.IndexInto(
                source,
                SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0))),
            SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0)));
        var replacement = SyntaxFactory.ParenthesizedExpression(comparison).WithTriviaFrom(invocation);
        return document.WithSyntaxRoot(root.ReplaceNode(invocation, replacement));
    }
}

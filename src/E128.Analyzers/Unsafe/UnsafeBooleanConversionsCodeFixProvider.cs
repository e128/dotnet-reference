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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UnsafeBooleanConversionsCodeFixProvider))]
[Shared]
public sealed class UnsafeBooleanConversionsCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [UnsafeBooleanConversionsAnalyzer.DiagnosticId];

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
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        if (node is not InvocationExpressionSyntax
            {
                Expression: MemberAccessExpressionSyntax { Name: GenericNameSyntax generic },
            } invocation
            || invocation.ArgumentList.Arguments.Count != 1
            || generic.TypeArgumentList.Arguments.Count != 2)
        {
            return;
        }

        var source = generic.TypeArgumentList.Arguments[0];
        var target = generic.TypeArgumentList.Arguments[1];
        var text = BuildReplacement(
            source.ToString(),
            target.ToString(),
            invocation.ArgumentList.Arguments[0].Expression);
        if (text is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Replace with a managed conversion (any nonzero int becomes true)",
                _ => Task.FromResult(ApplyFix(context.Document, root, invocation, text)),
                nameof(UnsafeBooleanConversionsCodeFixProvider)),
            diagnostic);
    }

    private static string? BuildReplacement(string source, string target, ExpressionSyntax value)
    {
        var valueText = value is IdentifierNameSyntax or MemberAccessExpressionSyntax or InvocationExpressionSyntax or ElementAccessExpressionSyntax
            ? value.ToString()
            : "(" + value.WithoutTrivia().ToFullString() + ")";
        var isIntToBool = string.Equals(source, "int", StringComparison.Ordinal)
            && string.Equals(target, "bool", StringComparison.Ordinal);
        var isBoolToInt = string.Equals(source, "bool", StringComparison.Ordinal)
            && string.Equals(target, "int", StringComparison.Ordinal);

        return isIntToBool
            ? "(" + valueText + " != 0)"
            : isBoolToInt ? "(" + valueText + " ? 1 : 0)" : null;
    }

    private static Document ApplyFix(
        Document document,
        SyntaxNode root,
        InvocationExpressionSyntax invocation,
        string text)
    {
        var replacement = SyntaxFactory.ParseExpression(text).WithTriviaFrom(invocation);
        return document.WithSyntaxRoot(root.ReplaceNode(invocation, replacement));
    }
}

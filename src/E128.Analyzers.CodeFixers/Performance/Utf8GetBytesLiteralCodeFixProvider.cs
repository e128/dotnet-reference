using System;
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
///     Code fix for E128141: replaces <c lang="csharp">Encoding.UTF8.GetBytes("...")</c> with a
///     <c lang="csharp">u8</c> literal, and keeps the <c lang="csharp">byte[]</c> shape with
///     <c lang="csharp">ToArray()</c> when the target needs an array.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Utf8GetBytesLiteralCodeFixProvider))]
[Shared]
public sealed class Utf8GetBytesLiteralCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [Utf8GetBytesLiteralAnalyzer.DiagnosticId];

    public override FixAllProvider? GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);

        if (node is not InvocationExpressionSyntax { ArgumentList.Arguments.Count: 1 } invocation
            || invocation.ArgumentList.Arguments[0].Expression is not LiteralExpressionSyntax literal
            || !literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return Task.CompletedTask;
        }

        var useToArray = diagnostic.Properties.TryGetValue(Utf8GetBytesLiteralAnalyzer.UseToArrayKey, out var value)
            && string.Equals(value, "true", StringComparison.Ordinal);

        context.RegisterCodeFix(
            CodeAction.Create(
                useToArray ? "Use a u8 literal with ToArray" : "Use a u8 literal",
                _ => Task.FromResult(ApplyFix(context.Document, root, invocation, literal, useToArray)),
                nameof(Utf8GetBytesLiteralCodeFixProvider)),
            diagnostic);

        return Task.CompletedTask;
    }

    private static Document ApplyFix(
        Document document,
        SyntaxNode root,
        InvocationExpressionSyntax invocation,
        LiteralExpressionSyntax literal,
        bool useToArray)
    {
        var literalText = literal.ToString();
        var text = literalText + (useToArray ? "u8.ToArray()" : "u8");
        var replacement = SyntaxFactory.ParseExpression(text).WithTriviaFrom(invocation);
        var newRoot = root.ReplaceNode(invocation, replacement);

        if (useToArray)
        {
            newRoot = UsingDirectives.AddIfMissing(newRoot, "System");
        }

        return document.WithSyntaxRoot(newRoot);
    }
}

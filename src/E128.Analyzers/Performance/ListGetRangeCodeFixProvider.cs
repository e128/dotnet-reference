using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Performance;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ListGetRangeCodeFixProvider))]
[Shared]
public sealed class ListGetRangeCodeFixProvider : CodeFixProvider
{
    private const string InteropNamespace = "System.Runtime.InteropServices";

    public override ImmutableArray<string> FixableDiagnosticIds => [ListGetRangeAnalyzer.DiagnosticId];

    public override FixAllProvider GetFixAllProvider()
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

        var invocation = root.FindNode(context.Diagnostics[0].Location.SourceSpan)
            .AncestorsAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault();

        if (invocation is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Use CollectionsMarshal.AsSpan with Slice",
                ct => ApplyFixAsync(context.Document, invocation, ct),
                ListGetRangeAnalyzer.DiagnosticId),
            context.Diagnostics[0]);
    }

    private static async Task<Document> ApplyFixAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return document;
        }

        var receiver = memberAccess.Expression;
        var startArg = invocation.ArgumentList.Arguments[0].Expression;
        var countArg = invocation.ArgumentList.Arguments[1].Expression;

        // Build: CollectionsMarshal.AsSpan(list).Slice(start, count).ToArray()
        var asSpanCall = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName("CollectionsMarshal"),
                SyntaxFactory.IdentifierName("AsSpan")),
            SyntaxFactory.ArgumentList(
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.Argument(receiver))));

        var sliceCall = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                asSpanCall,
                SyntaxFactory.IdentifierName("Slice")),
            SyntaxFactory.ArgumentList(
                SyntaxFactory.SeparatedList(new[]
                {
                    SyntaxFactory.Argument(startArg),
                    SyntaxFactory.Argument(countArg)
                })));

        var toArrayCall = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                sliceCall,
                SyntaxFactory.IdentifierName("ToArray")));

        var newRoot = AddUsingIfMissing(root.ReplaceNode(invocation, toArrayCall.WithTriviaFrom(invocation)));
        return document.WithSyntaxRoot(newRoot);
    }

    private static SyntaxNode AddUsingIfMissing(SyntaxNode root)
    {
        if (root is not CompilationUnitSyntax compilationUnit)
        {
            return root;
        }

        if (compilationUnit.Usings.Any(u =>
                string.Equals(u.Name?.ToString(), InteropNamespace, StringComparison.Ordinal)))
        {
            return root;
        }

        var newUsing = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(InteropNamespace))
            .WithTrailingTrivia(SyntaxFactory.ElasticLineFeed);

        var insertIndex = 0;
        for (var i = 0; i < compilationUnit.Usings.Count; i++)
        {
            if (StringComparer.Ordinal.Compare(compilationUnit.Usings[i].Name?.ToString(), InteropNamespace) < 0)
            {
                insertIndex = i + 1;
            }
        }

        return compilationUnit.WithUsings(compilationUnit.Usings.Insert(insertIndex, newUsing));
    }
}

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
using Microsoft.CodeAnalysis.Editing;

namespace E128.Analyzers.Performance;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(HttpClientResponseHeadersReadCodeFixProvider))]
[Shared]
public sealed class HttpClientResponseHeadersReadCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [HttpClientResponseHeadersReadAnalyzer.DiagnosticId];

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

        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        if (node is not InvocationExpressionSyntax invocation)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Add HttpCompletionOption.ResponseHeadersRead",
                ct => InsertResponseHeadersReadAsync(context.Document, invocation, ct),
                nameof(HttpClientResponseHeadersReadCodeFixProvider)),
            diagnostic);
    }

    private static async Task<Document> InsertResponseHeadersReadAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return document;
        }

        var responseHeadersReadArg = SyntaxFactory.Argument(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName("HttpCompletionOption"),
                SyntaxFactory.IdentifierName("ResponseHeadersRead")));

        var args = invocation.ArgumentList.Arguments;
        var insertIndex = FindInsertionIndex(semanticModel, args, cancellationToken);

        var newArgs = args.Insert(insertIndex, responseHeadersReadArg);
        var newArgList = invocation.ArgumentList.WithArguments(newArgs);
        var newInvocation = invocation.WithArgumentList(newArgList);

        var newRoot = root.ReplaceNode(invocation, newInvocation);

        var editor = await DocumentEditor.CreateAsync(
            document.WithSyntaxRoot(newRoot), cancellationToken).ConfigureAwait(false);

        var compilation = semanticModel.Compilation;
        var httpCompletionOptionType = compilation.GetTypeByMetadataName("System.Net.Http.HttpCompletionOption");
        if (httpCompletionOptionType is not null)
        {
            var currentRoot = editor.GetChangedRoot();
            if (currentRoot is CompilationUnitSyntax compilationUnit)
            {
                var hasUsing = compilationUnit.Usings.Any(u =>
                    string.Equals(u.Name?.ToString(), "System.Net.Http", StringComparison.Ordinal));

                if (!hasUsing)
                {
                    var newUsing = SyntaxFactory.UsingDirective(
                            SyntaxFactory.ParseName("System.Net.Http"))
                        .NormalizeWhitespace()
                        .WithTrailingTrivia(SyntaxFactory.LineFeed);

                    editor.ReplaceNode(compilationUnit,
                        compilationUnit.AddUsings(newUsing));
                }
            }
        }

        return editor.GetChangedDocument();
    }

    private static int FindInsertionIndex(
        SemanticModel semanticModel,
        SeparatedSyntaxList<ArgumentSyntax> args,
        CancellationToken cancellationToken)
    {
        if (!args.Any())
        {
            return 0;
        }

        var lastArg = args.Last();
        var lastArgType = semanticModel.GetTypeInfo(lastArg.Expression, cancellationToken).Type;
        return lastArgType is not null
               && string.Equals(lastArgType.ToDisplayString(), "System.Threading.CancellationToken", StringComparison.Ordinal)
            ? args.Count - 1
            : args.Count;
    }
}

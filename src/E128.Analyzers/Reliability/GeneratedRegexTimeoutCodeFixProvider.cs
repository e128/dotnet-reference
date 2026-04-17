using System;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Reliability;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(GeneratedRegexTimeoutCodeFixProvider))]
[Shared]
public sealed class GeneratedRegexTimeoutCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [GeneratedRegexAnalyzer.TimeoutDiagnosticId];

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

        if (node is not AttributeSyntax)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Add matchTimeoutMilliseconds: Timeout.Infinite",
                ct => ApplyFixAsync(context.Document, node, ct),
                nameof(GeneratedRegexTimeoutCodeFixProvider)),
            diagnostic);
    }

    private static async Task<Document> ApplyFixAsync(
        Document document,
        SyntaxNode attributeNode,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var attribute = (AttributeSyntax)attributeNode;
        var newAttribute = BuildAttributeWithTimeout(attribute);

        var newRoot = root.ReplaceNode(attribute, newAttribute);

        return await EnsureUsingDirectiveAsync(document.WithSyntaxRoot(newRoot), "System.Threading", cancellationToken)
            .ConfigureAwait(false);
    }

    private static AttributeSyntax BuildAttributeWithTimeout(AttributeSyntax attribute)
    {
        var timeoutArg = SyntaxFactory.AttributeArgument(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName("Timeout"),
                    SyntaxFactory.IdentifierName("Infinite")))
            .WithLeadingTrivia(SyntaxFactory.Space);

        var positionalCount = CountPositionalArgs(attribute);

        if (positionalCount < 2)
        {
            var optionsArg = SyntaxFactory.AttributeArgument(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName("RegexOptions"),
                        SyntaxFactory.IdentifierName("None")))
                .WithLeadingTrivia(SyntaxFactory.Space);

            if (attribute.ArgumentList is { } argList)
            {
                return attribute.WithArgumentList(argList.AddArguments(optionsArg, timeoutArg));
            }

            var newList = SyntaxFactory.AttributeArgumentList(
                SyntaxFactory.SeparatedList([optionsArg, timeoutArg]));
            return attribute.WithArgumentList(newList);
        }

        return attribute.WithArgumentList(attribute.ArgumentList!.AddArguments(timeoutArg));
    }

    private static int CountPositionalArgs(AttributeSyntax attribute)
    {
        if (attribute.ArgumentList is null)
        {
            return 0;
        }

        var count = 0;
        foreach (var arg in attribute.ArgumentList.Arguments)
        {
            if (arg.NameColon is null && arg.NameEquals is null)
            {
                count++;
            }
        }

        return count;
    }

    private static async Task<Document> EnsureUsingDirectiveAsync(
        Document document,
        string namespaceName,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is not CompilationUnitSyntax compilationUnit)
        {
            return document;
        }

        foreach (var existing in compilationUnit.Usings)
        {
            if (string.Equals(existing.Name?.ToString(), namespaceName, StringComparison.Ordinal))
            {
                return document;
            }
        }

        var usingDirective = SyntaxFactory.UsingDirective(
            SyntaxFactory.ParseName(namespaceName));

        var newCompilationUnit = compilationUnit.AddUsings(usingDirective);
        return document.WithSyntaxRoot(newCompilationUnit);
    }
}

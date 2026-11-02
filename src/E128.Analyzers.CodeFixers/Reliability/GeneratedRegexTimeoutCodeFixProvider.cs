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

namespace E128.Analyzers.Reliability;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(GeneratedRegexTimeoutCodeFixProvider))]
[Shared]
public sealed class GeneratedRegexTimeoutCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [GeneratedRegexAnalyzer.TimeoutDiagnosticId];

    public override FixAllProvider? GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        // A positional argument cannot follow a named one, so an attribute that already uses a named
        // argument cannot take the appended positional pair.
        if (node is not AttributeSyntax attribute || HasNamedArgument(attribute))
        {
            return Task.CompletedTask;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Add matchTimeoutMilliseconds: Timeout.Infinite",
                ct => ApplyFixAsync(context.Document, node, ct),
                nameof(GeneratedRegexTimeoutCodeFixProvider)),
            diagnostic);

        return Task.CompletedTask;
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

        var withTimeoutUsing = await EnsureUsingDirectiveAsync(
                document.WithSyntaxRoot(newRoot), "System.Threading", cancellationToken)
            .ConfigureAwait(false);

        // The emitted options value needs its namespace when the attribute was written fully
        // qualified.
        return await EnsureUsingDirectiveAsync(
                withTimeoutUsing, "System.Text.RegularExpressions", cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool HasNamedArgument(AttributeSyntax attribute)
    {
        return attribute.ArgumentList?.Arguments.Any(
                   argument => argument.NameColon is not null || argument.NameEquals is not null)
               ?? false;
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

        return root is null ? document : document.WithSyntaxRoot(UsingDirectives.AddIfMissing(root, namespaceName));
    }
}

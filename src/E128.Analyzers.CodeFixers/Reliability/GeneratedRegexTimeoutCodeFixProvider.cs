using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Reliability;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(GeneratedRegexTimeoutCodeFixProvider))]
[Shared]
public sealed class GeneratedRegexTimeoutCodeFixProvider : E128CodeFixProviderBase
{
    private const int MaxPositionalArgsWithoutTimeout = 2;

    public override ImmutableArray<string> FixableDiagnosticIds =>
        [GeneratedRegexAnalyzer.TimeoutDiagnosticId];

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

        CodeFixRegistration.Register(context, diagnostic, "Add matchTimeoutMilliseconds: Timeout.Infinite", nameof(GeneratedRegexTimeoutCodeFixProvider), ct => ApplyFixAsync(context.Document, node, ct));

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

        var withTimeoutUsing = await UsingDirectives.EnsureAddedAsync(
                document.WithSyntaxRoot(newRoot), "System.Threading", cancellationToken)
            .ConfigureAwait(false);

        // The emitted options value needs its namespace when the attribute was written fully
        // qualified.
        return await UsingDirectives.EnsureAddedAsync(
                withTimeoutUsing, "System.Text.RegularExpressions", cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool HasNamedArgument(AttributeSyntax attribute)
    {
        return attribute.ArgumentList?.Arguments.Any(
                   static argument => argument.NameColon is not null || argument.NameEquals is not null)
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

        if (positionalCount < MaxPositionalArgsWithoutTimeout)
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
}

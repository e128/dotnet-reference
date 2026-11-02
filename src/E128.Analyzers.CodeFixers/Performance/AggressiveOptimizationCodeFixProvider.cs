using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Performance;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AggressiveOptimizationCodeFixProvider))]
[Shared]
public sealed class AggressiveOptimizationCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [AggressiveOptimizationAnalyzer.DiagnosticId];

    public override FixAllProvider? GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        if (node is not AttributeSyntax { Parent: AttributeListSyntax attributeList } attribute)
        {
            return Task.CompletedTask;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Remove AggressiveOptimization",
                _ => Task.FromResult(ApplyFix(context.Document, root, attribute, attributeList)),
                nameof(AggressiveOptimizationCodeFixProvider)),
            diagnostic);

        return Task.CompletedTask;
    }

    private static Document ApplyFix(
        Document document,
        SyntaxNode root,
        AttributeSyntax attribute,
        AttributeListSyntax attributeList)
    {
        var kept = new List<AttributeArgumentSyntax>();

        if (attribute.ArgumentList is not null)
        {
            foreach (var argument in attribute.ArgumentList.Arguments)
            {
                var stripped = StripAggressiveOptimization(argument.Expression);
                if (stripped is not null)
                {
                    kept.Add(argument.WithExpression(stripped));
                }
            }
        }

        if (kept.Count == 0)
        {
            var withoutAttribute = attributeList.Attributes.Count == 1
                ? RemoveAttributeList(root, attributeList)
                : root.ReplaceNode(
                    attributeList,
                    attributeList.WithAttributes(attributeList.Attributes.Remove(attribute)));

            return document.WithSyntaxRoot(withoutAttribute);
        }

        var argumentList = attribute.ArgumentList?.WithArguments(SyntaxFactory.SeparatedList(kept));

        return document.WithSyntaxRoot(
            root.ReplaceNode(attribute, attribute.WithArgumentList(argumentList)));
    }

    // The first attribute list owns the member's doc comment and indentation, so hand them to the next token.
    private static SyntaxNode RemoveAttributeList(SyntaxNode root, AttributeListSyntax attributeList)
    {
        if (attributeList.Parent is not MemberDeclarationSyntax member || member.AttributeLists[0] != attributeList)
        {
            return root.RemoveNode(attributeList, SyntaxRemoveOptions.KeepNoTrivia) ?? root;
        }

        var trimmed = member.WithAttributeLists(member.AttributeLists.Remove(attributeList));
        var first = trimmed.GetFirstToken();
        trimmed = trimmed.ReplaceToken(first, first.WithLeadingTrivia(attributeList.GetLeadingTrivia()));

        return root.ReplaceNode(member, trimmed);
    }

    private static ExpressionSyntax? StripAggressiveOptimization(ExpressionSyntax expression)
    {
        if (IsAggressiveOptimization(expression))
        {
            return null;
        }

        if (expression is not BinaryExpressionSyntax binary
            || !binary.IsKind(SyntaxKind.BitwiseOrExpression))
        {
            return expression;
        }

        var left = StripAggressiveOptimization(binary.Left);
        var right = StripAggressiveOptimization(binary.Right);

        return left is null
            ? right?.WithoutTrailingTrivia()
            : right is null
                ? left.WithoutTrailingTrivia()
                : SyntaxFactory.BinaryExpression(SyntaxKind.BitwiseOrExpression, left, right);
    }

    private static bool IsAggressiveOptimization(ExpressionSyntax expression)
    {
        // A parenthesized option must match too, or the fix keeps it and changes nothing.
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        var name = expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax { Name: IdentifierNameSyntax member } => member.Identifier.ValueText,
            _ => null,
        };

        return string.Equals(name, "AggressiveOptimization", StringComparison.Ordinal);
    }
}

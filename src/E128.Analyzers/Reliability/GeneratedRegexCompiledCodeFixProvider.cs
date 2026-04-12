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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(GeneratedRegexCompiledCodeFixProvider))]
[Shared]
public sealed class GeneratedRegexCompiledCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [GeneratedRegexAnalyzer.CompiledDiagnosticId];

    public override FixAllProvider? GetFixAllProvider() =>
        WellKnownFixAllProviders.BatchFixer;

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
                title: "Remove redundant RegexOptions.Compiled",
                createChangedDocument: ct => ApplyFixAsync(context.Document, node, ct),
                equivalenceKey: nameof(GeneratedRegexCompiledCodeFixProvider)),
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

        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return document;
        }

        var attribute = (AttributeSyntax)attributeNode;
        if (attribute.ArgumentList is null)
        {
            return document;
        }

        var optionsArg = FindOptionsArgument(attribute);
        if (optionsArg is null)
        {
            return document;
        }

        var newExpression = RemoveCompiled(optionsArg.Expression, semanticModel, cancellationToken);
        if (newExpression is null)
        {
            return document;
        }

        var newArg = optionsArg.WithExpression(newExpression);
        var newRoot = root.ReplaceNode(optionsArg, newArg);
        return document.WithSyntaxRoot(newRoot);
    }

    private static AttributeArgumentSyntax? FindOptionsArgument(AttributeSyntax attribute)
    {
        var positionalIndex = 0;
        foreach (var arg in attribute.ArgumentList!.Arguments)
        {
            if (arg.NameColon is { } nameColon
                && string.Equals(nameColon.Name.Identifier.ValueText, "options", StringComparison.Ordinal))
            {
                return arg;
            }

            if (arg.NameColon is null && arg.NameEquals is null)
            {
                if (positionalIndex == 1)
                {
                    return arg;
                }

                positionalIndex++;
            }
        }

        return null;
    }

    private static ExpressionSyntax? RemoveCompiled(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        return IsCompiledReference(expression, semanticModel, cancellationToken)
            ? CreateRegexOptionsNone(expression)
            : expression is BinaryExpressionSyntax binary
            && binary.IsKind(SyntaxKind.BitwiseOrExpression)
            ? RemoveCompiledFromBinary(binary, expression, semanticModel, cancellationToken)
            : expression;
    }

    private static ExpressionSyntax? RemoveCompiledFromBinary(
        BinaryExpressionSyntax binary,
        ExpressionSyntax originalExpression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var leftIsCompiled = IsCompiledReference(binary.Left, semanticModel, cancellationToken);
        var rightIsCompiled = IsCompiledReference(binary.Right, semanticModel, cancellationToken);

        if (leftIsCompiled && rightIsCompiled)
        {
            return CreateRegexOptionsNone(originalExpression);
        }

        if (leftIsCompiled)
        {
            return binary.Right.WithTriviaFrom(originalExpression);
        }

        if (rightIsCompiled)
        {
            return binary.Left.WithTriviaFrom(originalExpression);
        }

        var newLeft = RemoveCompiled(binary.Left, semanticModel, cancellationToken);
        if (newLeft is not null && newLeft != binary.Left)
        {
            return binary.WithLeft(newLeft).WithTriviaFrom(originalExpression);
        }

        var newRight = RemoveCompiled(binary.Right, semanticModel, cancellationToken);
        return newRight is not null && newRight != binary.Right ? binary.WithRight(newRight).WithTriviaFrom(originalExpression) : (ExpressionSyntax?)null;
    }

    private static MemberAccessExpressionSyntax CreateRegexOptionsNone(ExpressionSyntax triviaSource) =>
        SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            SyntaxFactory.IdentifierName("RegexOptions"),
            SyntaxFactory.IdentifierName("None"))
            .WithTriviaFrom(triviaSource);

    private static bool IsCompiledReference(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        if (expression is not MemberAccessExpressionSyntax memberAccess
            || !string.Equals(memberAccess.Name.Identifier.ValueText, "Compiled", StringComparison.Ordinal))
        {
            return false;
        }

        var symbol = semanticModel.GetSymbolInfo(memberAccess, cancellationToken).Symbol;
        return symbol is IFieldSymbol { ContainingType: { } containingType }
            && string.Equals(containingType.Name, "RegexOptions", StringComparison.Ordinal)
            && containingType.ContainingNamespace is { Name: "RegularExpressions" }
            && containingType.ContainingNamespace.ContainingNamespace is { Name: "Text" }
            && containingType.ContainingNamespace.ContainingNamespace.ContainingNamespace is { Name: "System" }
            && containingType.ContainingNamespace.ContainingNamespace.ContainingNamespace.ContainingNamespace?.IsGlobalNamespace == true;
    }
}

using System;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Reliability;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(GeneratedRegexCompiledCodeFixProvider))]
[Shared]
public sealed class GeneratedRegexCompiledCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [GeneratedRegexAnalyzer.CompiledDiagnosticId];

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        RegisterNodeFix<AttributeSyntax>(
            context,
            root,
            "Remove redundant RegexOptions.Compiled",
            nameof(GeneratedRegexCompiledCodeFixProvider),
            (attribute, ct) => ApplyFixAsync(context.Document, attribute, ct));

        return Task.CompletedTask;
    }

    private static async Task<Document> ApplyFixAsync(
        Document document,
        AttributeSyntax attribute,
        CancellationToken cancellationToken)
    {
        var rootAndModel = await FixSyntax.GetRootAndSemanticModelAsync(document, cancellationToken).ConfigureAwait(false);
        if (rootAndModel is null)
        {
            return document;
        }

        var root = rootAndModel.Value.Root;
        var semanticModel = rootAndModel.Value.SemanticModel;

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
                if (positionalIndex is 1)
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
        if (IsCompiledReference(expression, semanticModel, cancellationToken))
        {
            return CreateRegexOptionsNone(expression);
        }

        if (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            return RemoveCompiledFromParenthesized(parenthesized, semanticModel, cancellationToken);
        }

        if (expression is BinaryExpressionSyntax binary
            && binary.IsKind(SyntaxKind.BitwiseOrExpression))
        {
            return RemoveCompiledFromBinary(binary, expression, semanticModel, cancellationToken);
        }

        return expression;
    }

    private static ExpressionSyntax RemoveCompiledFromParenthesized(
        ParenthesizedExpressionSyntax parenthesized,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var inner = RemoveCompiled(parenthesized.Expression, semanticModel, cancellationToken);
        if (inner is null || inner == parenthesized.Expression)
        {
            return parenthesized;
        }

        return inner is MemberAccessExpressionSyntax
            ? inner.WithTriviaFrom(parenthesized)
            : parenthesized.WithExpression(inner);
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

    private static MemberAccessExpressionSyntax CreateRegexOptionsNone(ExpressionSyntax triviaSource)
    {
        return SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName("RegexOptions"),
                SyntaxFactory.IdentifierName("None"))
            .WithTriviaFrom(triviaSource);
    }

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
               && IsSystemTextRegularExpressions(containingType.ContainingNamespace);
    }

    private static bool IsSystemTextRegularExpressions(INamespaceSymbol? regularExpressions)
    {
        return regularExpressions is not null
               && string.Equals(regularExpressions.Name, "RegularExpressions", StringComparison.Ordinal)
               && regularExpressions.ContainingNamespace is { } text
               && string.Equals(text.Name, "Text", StringComparison.Ordinal)
               && text.ContainingNamespace is { } system
               && string.Equals(system.Name, "System", StringComparison.Ordinal)
               && system.ContainingNamespace is { IsGlobalNamespace: true };
    }
}

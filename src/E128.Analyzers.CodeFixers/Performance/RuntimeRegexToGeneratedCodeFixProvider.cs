using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Performance;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RuntimeRegexToGeneratedCodeFixProvider))]
[Shared]
public sealed class RuntimeRegexToGeneratedCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [RuntimeRegexToGeneratedAnalyzer.DiagnosticId];

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

        if (node is not FieldDeclarationSyntax field
            || field.Declaration.Variables.Count != 1
            || field.AttributeLists.Count > 0
            || field.Parent is not TypeDeclarationSyntax)
        {
            return;
        }

        var creation = RuntimeRegexToGeneratedAnalyzer.TryGetCreation(field.Declaration.Variables[0]);
        if (creation?.ArgumentList is not { Arguments.Count: 1 or 2 } argumentList)
        {
            return;
        }

        // GeneratedRegex arguments must be constants, and the options must be a plain flag list.
        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel is null
            || argumentList.Arguments.Any(argument =>
                !semanticModel.GetConstantValue(argument.Expression, context.CancellationToken).HasValue)
            || (argumentList.Arguments.Count == 2 && StripCompiled(argumentList.Arguments[1].Expression) is null))
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Convert to [GeneratedRegex]",
                _ => Task.FromResult(ApplyFix(context.Document, root, field, argumentList)),
                nameof(RuntimeRegexToGeneratedCodeFixProvider)),
            diagnostic);
    }

    private static Document ApplyFix(
        Document document,
        SyntaxNode root,
        FieldDeclarationSyntax field,
        ArgumentListSyntax argumentList)
    {
        var arguments = argumentList.Arguments;
        var pattern = arguments[0].Expression.ToString();
        var options = arguments.Count == 2 ? StripCompiled(arguments[1].Expression) : "RegexOptions.None";
        if (options is null)
        {
            return document;
        }

        var propertyName = field.Declaration.Variables[0].Identifier.ValueText;

        var modifierTexts = new List<string>();
        foreach (var modifier in field.Modifiers)
        {
            if (modifier.IsKind(SyntaxKind.ReadOnlyKeyword))
            {
                continue;
            }

            modifierTexts.Add(modifier.ValueText);
        }

        var modifiers = string.Join(" ", modifierTexts) + " ";
        var fieldType = field.Declaration.Type.ToString();
        var indent = GetIndent(field.GetLeadingTrivia());

        var text = "[System.Text.RegularExpressions.GeneratedRegex("
            + pattern
            + ", "
            + options
            + ", matchTimeoutMilliseconds: System.Threading.Timeout.Infinite)]\n"
            + indent
            + modifiers
            + "partial "
            + fieldType
            + " "
            + propertyName
            + " { get; }";

        var property = SyntaxFactory.ParseMemberDeclaration(text);
        if (property is not PropertyDeclarationSyntax)
        {
            return document;
        }

        var annotation = new SyntaxAnnotation();
        var replacement = property
            .WithLeadingTrivia(field.GetLeadingTrivia())
            .WithTrailingTrivia(field.GetTrailingTrivia())
            .WithAdditionalAnnotations(annotation);

        var newRoot = root.ReplaceNode(field, replacement);
        newRoot = MakeEnclosingTypesPartial(newRoot, annotation);

        return document.WithSyntaxRoot(newRoot);
    }

    private static string GetIndent(SyntaxTriviaList leadingTrivia)
    {
        var indent = string.Empty;
        foreach (var trivia in leadingTrivia)
        {
            if (trivia.IsKind(SyntaxKind.WhitespaceTrivia))
            {
                indent = trivia.ToFullString();
            }
        }

        return indent;
    }

    private static SyntaxNode MakeEnclosingTypesPartial(SyntaxNode root, SyntaxAnnotation annotation)
    {
        var target = root.GetAnnotatedNodes(annotation).FirstOrDefault();
        if (target is null)
        {
            return root;
        }

        var types = target.Ancestors().OfType<TypeDeclarationSyntax>().ToList();

        return types.Count == 0
            ? root
            : root.ReplaceNodes(types, (_, rewritten) => AddPartial(rewritten));
    }

    private static TypeDeclarationSyntax AddPartial(TypeDeclarationSyntax type)
    {
        if (type.Modifiers.Any(SyntaxKind.PartialKeyword))
        {
            return type;
        }

        var partialToken = SyntaxFactory.Token(SyntaxKind.PartialKeyword).WithTrailingTrivia(SyntaxFactory.Space);

        if (type.Modifiers.Count > 0)
        {
            // `partial` must sit directly before the type keyword.
            return type.AddModifiers(partialToken);
        }

        // No modifiers: the keyword owns the indentation and doc comments, so move them to `partial`.
        var keyword = type.Keyword;
        return type
            .WithKeyword(keyword.WithLeadingTrivia(SyntaxFactory.TriviaList()))
            .WithModifiers(SyntaxFactory.TokenList(partialToken.WithLeadingTrivia(keyword.LeadingTrivia)));
    }

    private static string? StripCompiled(ExpressionSyntax options)
    {
        var flags = new List<string>();
        if (!CollectFlags(options, flags))
        {
            return null;
        }

        flags.RemoveAll(flag => flag.EndsWith("Compiled", System.StringComparison.Ordinal));
        return flags.Count == 0 ? "RegexOptions.None" : string.Join(" | ", flags);
    }

    private static bool CollectFlags(ExpressionSyntax expression, List<string> flags)
    {
        switch (expression)
        {
            case ParenthesizedExpressionSyntax parenthesized:
                return CollectFlags(parenthesized.Expression, flags);
            case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.BitwiseOrExpression):
                return CollectFlags(binary.Left, flags) && CollectFlags(binary.Right, flags);
            case MemberAccessExpressionSyntax or IdentifierNameSyntax:
                flags.Add(expression.ToString());
                return true;
            default:
                return false;
        }
    }
}

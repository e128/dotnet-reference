using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Performance;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RuntimeRegexToGeneratedCodeFixProvider))]
[Shared]
public sealed class RuntimeRegexToGeneratedCodeFixProvider : E128CodeFixProviderBase
{
    private const int ArgumentCountWithOptions = 2;

    public override ImmutableArray<string> FixableDiagnosticIds =>
        [RuntimeRegexToGeneratedAnalyzer.DiagnosticId];

    protected override async Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        if (GetCandidateField(node) is not { } field)
        {
            return;
        }

        var creation = RuntimeRegexToGeneratedAnalyzer.TryGetCreation(field.Declaration.Variables[0]);
        if (creation?.ArgumentList is not { Arguments.Count: 1 or ArgumentCountWithOptions } argumentList)
        {
            return;
        }

        // GeneratedRegex arguments must be constants, and the options must be a plain flag list.
        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel is null
            || !IsConvertible(argumentList, semanticModel, context.CancellationToken))
        {
            return;
        }

        CodeFixRegistration.Register(context, diagnostic, "Convert to [GeneratedRegex]", nameof(RuntimeRegexToGeneratedCodeFixProvider), _ => Task.FromResult(ApplyFix(context.Document, root, field, argumentList)));
    }

    private static FieldDeclarationSyntax? GetCandidateField(SyntaxNode node)
    {
        return node is FieldDeclarationSyntax { Parent: TypeDeclarationSyntax, AttributeLists.Count: 0 } field
               && field.Declaration.Variables.Count is 1
            ? field
            : null;
    }

    private static bool IsConvertible(
        ArgumentListSyntax argumentList,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var arguments = argumentList.Arguments;
        return arguments.All(argument => semanticModel.GetConstantValue(argument.Expression, cancellationToken).HasValue)
               && (arguments.Count is not ArgumentCountWithOptions || StripCompiled(arguments[1].Expression) is not null);
    }

    private static Document ApplyFix(
        Document document,
        SyntaxNode root,
        FieldDeclarationSyntax field,
        ArgumentListSyntax argumentList)
    {
        var arguments = argumentList.Arguments;
        var pattern = arguments[0].Expression.ToString();
        var options = arguments.Count is ArgumentCountWithOptions ? StripCompiled(arguments[1].Expression) : "RegexOptions.None";
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
        var indent = FixSyntax.LastIndentation(field.GetLeadingTrivia());

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

    private static SyntaxNode MakeEnclosingTypesPartial(SyntaxNode root, SyntaxAnnotation annotation)
    {
        var target = root.GetAnnotatedNodes(annotation).FirstOrDefault();
        if (target is null)
        {
            return root;
        }

        var types = target.Ancestors().OfType<TypeDeclarationSyntax>().ToList();

        return types.Count is 0
            ? root
            : root.ReplaceNodes(types, static (_, rewritten) => AddPartial(rewritten));
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

        _ = flags.RemoveAll(static flag => flag.EndsWith("Compiled", System.StringComparison.Ordinal));
        return flags.Count is 0 ? "RegexOptions.None" : string.Join(" | ", flags);
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

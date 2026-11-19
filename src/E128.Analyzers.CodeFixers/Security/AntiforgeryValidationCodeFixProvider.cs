using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Security;

/// <summary>
///     Code fix for E128242: adds <c lang="csharp">[ValidateAntiForgeryToken]</c> above the
///     action and adds <c lang="csharp">using Microsoft.AspNetCore.Mvc;</c> when absent.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AntiforgeryValidationCodeFixProvider))]
[Shared]
public sealed class AntiforgeryValidationCodeFixProvider : E128CodeFixProviderBase
{
    private const string AttributeName = "ValidateAntiForgeryToken";

    private const string Namespace = "Microsoft.AspNetCore.Mvc";

    public override ImmutableArray<string> FixableDiagnosticIds =>
        [AntiforgeryValidationAnalyzer.DiagnosticId];

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);

        if (node.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().FirstOrDefault() is not { } method
            || HasAttribute(method))
        {
            return Task.CompletedTask;
        }

        var newMethod = InsertAttribute(method);
        var newRoot = root.ReplaceNode(method, newMethod);
        newRoot = UsingDirectives.AddIfMissing(newRoot, Namespace);

        CodeFixRegistration.Register(
            context,
            diagnostic,
            "Add [ValidateAntiForgeryToken]",
            nameof(AntiforgeryValidationCodeFixProvider),
            _ => Task.FromResult(context.Document.WithSyntaxRoot(newRoot)));

        return Task.CompletedTask;
    }

    private static bool HasAttribute(MethodDeclarationSyntax method)
    {
        return method.AttributeLists
            .SelectMany(static list => list.Attributes)
            .Any(static attribute => string.Equals(
                attribute.Name.ToString(),
                AttributeName,
                System.StringComparison.Ordinal));
    }

    /// <summary>
    ///     Inserts one attribute list above the existing attributes. The first list keeps its
    ///     indentation on the new line, and the original leading trivia moves to the new list so
    ///     a comment stays above the whole attribute block.
    /// </summary>
    private static MethodDeclarationSyntax InsertAttribute(MethodDeclarationSyntax method)
    {
        var attribute = SyntaxFactory.AttributeList(
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.Attribute(SyntaxFactory.IdentifierName(AttributeName))))
            .WithLeadingTrivia(method.GetLeadingTrivia())
            .WithTrailingTrivia(
                SyntaxFactory.LineFeed,
                SyntaxFactory.Whitespace(FixSyntax.MemberIndentation(method)));

        var attributeLists = method.AttributeLists;
        if (attributeLists.Count is 0)
        {
            return method
                .WithLeadingTrivia(SyntaxFactory.TriviaList())
                .WithAttributeLists(SyntaxFactory.SingletonList(attribute));
        }

        var first = attributeLists[0].WithLeadingTrivia(SyntaxFactory.TriviaList());
        return method.WithAttributeLists(attributeLists.Replace(attributeLists[0], first).Insert(0, attribute));
    }
}

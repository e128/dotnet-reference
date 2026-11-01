using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Design;

/// <summary>
///     Code fix for E128050: replaces the numeric type (int, long, float, double) with <c lang="csharp">TimeSpan</c>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TimeSpanForDurationCodeFixProvider))]
[Shared]
public sealed class TimeSpanForDurationCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [TimeSpanForDurationAnalyzer.DiagnosticId];

    public override FixAllProvider? GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        // A parameter default is a compile-time constant. An int literal is not one for TimeSpan, so
        // changing the type alone gives CS1750, and `default` would change the default from seconds to
        // zero. The developer has to pick the duration, so no fix is offered.
        if (node.FirstAncestorOrSelf<ParameterSyntax>() is { Default: not null })
        {
            return Task.CompletedTask;
        }

        // A property initializer is a number too. Changing the type alone leaves `= 5000` next to
        // TimeSpan, which does not compile.
        if (node.FirstAncestorOrSelf<PropertyDeclarationSyntax>() is { Initializer: not null })
        {
            return Task.CompletedTask;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Change type to TimeSpan",
                ct => ApplyFixAsync(context.Document, root, node, ct),
                nameof(TimeSpanForDurationCodeFixProvider)),
            diagnostic);

        return Task.CompletedTask;
    }

    private static Task<Document> ApplyFixAsync(
        Document document,
        SyntaxNode root,
        SyntaxNode node,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        var timeSpanType = SyntaxFactory.IdentifierName("TimeSpan");

        SyntaxNode newRoot;
        if (node is PropertyDeclarationSyntax property)
        {
            var newProperty = property.WithType(timeSpanType.WithTriviaFrom(property.Type));
            newRoot = root.ReplaceNode(property, newProperty);
        }
        else if (node is ParameterSyntax param && param.Type is not null)
        {
            var newParam = param.WithType(timeSpanType.WithTriviaFrom(param.Type));
            newRoot = root.ReplaceNode(param, newParam);
        }
        else
        {
            return Task.FromResult(document);
        }

        newRoot = UsingDirectives.AddIfMissing(newRoot, "System");
        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }
}

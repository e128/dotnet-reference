using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace E128.Analyzers.Maintainability;

/// <summary>
///     Code fix for E128088: adds a throwing <c lang="csharp">default</c> arm to a switch
///     statement, or a throwing discard arm to a switch expression. A later enum member then
///     fails loudly instead of falling through.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SwitchEnumExhaustivenessCodeFixProvider))]
[Shared]
public sealed class SwitchEnumExhaustivenessCodeFixProvider : E128CodeFixProviderBase
{
    private const string DefaultSectionTemplate = """
                                                  switch (0)
                                                  {
                                                      default:
                                                          throw new System.ArgumentOutOfRangeException();
                                                  }
                                                  """;

    private const string DiscardArmTemplate = """
                                              0 switch
                                              {
                                                  _ => throw new System.ArgumentOutOfRangeException()
                                              }
                                              """;

    public override ImmutableArray<string> FixableDiagnosticIds =>
        [SwitchEnumExhaustivenessAnalyzer.DiagnosticId];

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);
        var target = node.FirstAncestorOrSelf<SyntaxNode>(
            candidate => candidate is SwitchStatementSyntax or SwitchExpressionSyntax);

        switch (target)
        {
            case SwitchStatementSyntax switchStatement:
                context.RegisterCodeFix(
                    CodeAction.Create(
                        "Add a throwing default arm",
                        ct => AddDefaultSectionAsync(context.Document, switchStatement, ct),
                        nameof(SwitchEnumExhaustivenessCodeFixProvider)),
                    diagnostic);
                return Task.CompletedTask;
            case SwitchExpressionSyntax switchExpression:
                context.RegisterCodeFix(
                    CodeAction.Create(
                        "Add a throwing default arm",
                        ct => AddDiscardArmAsync(context.Document, switchExpression, ct),
                        nameof(SwitchEnumExhaustivenessCodeFixProvider)),
                    diagnostic);
                return Task.CompletedTask;
            default:
                return Task.CompletedTask;
        }
    }

    private static async Task<Document> AddDefaultSectionAsync(
        Document document,
        SwitchStatementSyntax switchStatement,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var template = (SwitchStatementSyntax)SyntaxFactory.ParseStatement(DefaultSectionTemplate);
        var section = template.Sections[0].WithAdditionalAnnotations(Formatter.Annotation);

        return document.WithSyntaxRoot(root.ReplaceNode(switchStatement, switchStatement.AddSections(section)));
    }

    private static async Task<Document> AddDiscardArmAsync(
        Document document,
        SwitchExpressionSyntax switchExpression,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var template = (SwitchExpressionSyntax)SyntaxFactory.ParseExpression(DiscardArmTemplate);
        var arm = template.Arms[0].WithAdditionalAnnotations(Formatter.Annotation);

        return document.WithSyntaxRoot(
            root.ReplaceNode(switchExpression, switchExpression.WithArms(switchExpression.Arms.Add(arm))));
    }
}

using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Performance;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SequenceEqualLoopCodeFixProvider))]
[Shared]
public sealed class SequenceEqualLoopCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [SequenceEqualLoopAnalyzer.DiagnosticId];

    public override FixAllProvider? GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    protected override async Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        if (node is not ForStatementSyntax forStatement)
        {
            return;
        }

        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return;
        }

        var operands = SequenceEqualLoopAnalyzer.TryGetOperands(forStatement, semanticModel, context.CancellationToken);
        if (operands is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Use SequenceEqual",
                _ => Task.FromResult(ApplyFix(context.Document, root, forStatement, operands.Value)),
                nameof(SequenceEqualLoopCodeFixProvider)),
            diagnostic);
    }

    private static Document ApplyFix(
        Document document,
        SyntaxNode root,
        ForStatementSyntax forStatement,
        (ExpressionSyntax Left, ExpressionSyntax Right) operands)
    {
        if (forStatement.Parent is not BlockSyntax block)
        {
            return document;
        }

        var index = block.Statements.IndexOf(forStatement);
        if (index < 0 || index + 1 >= block.Statements.Count)
        {
            return document;
        }

        var left = operands.Left.ToString();
        var right = operands.Right.ToString();
        var text = "return " + left + ".AsSpan().SequenceEqual(" + right + ");";

        var replacement = SyntaxFactory.ParseStatement(text).WithTriviaFrom(forStatement);
        var statements = block.Statements.RemoveAt(index + 1);
        statements = statements.Replace(statements[index], replacement);

        var newRoot = root.ReplaceNode(block, block.WithStatements(statements));
        return document.WithSyntaxRoot(UsingDirectives.AddIfMissing(newRoot, "System"));
    }
}

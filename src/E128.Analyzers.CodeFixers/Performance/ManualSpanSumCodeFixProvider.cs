using System;
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
using Microsoft.CodeAnalysis.Text;

namespace E128.Analyzers.Performance;

/// <summary>
///     Code fix for E128135: replaces the reduction loop with one
///     <c lang="csharp">TensorPrimitives</c> call.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ManualSpanSumCodeFixProvider))]
[Shared]
public sealed class ManualSpanSumCodeFixProvider : E128CodeFixProviderBase
{
    private const string TensorsNamespace = "System.Numerics.Tensors";

    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [ManualSpanSumAnalyzer.DiagnosticId];

    /// <inheritdoc />
    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var loop = root.FindNode(context.Diagnostics[0].Location.SourceSpan, getInnermostNodeForTie: true)
            .AncestorsAndSelf()
            .OfType<ForEachStatementSyntax>()
            .FirstOrDefault();

        if (loop is null
            || !ManualSpanSumAnalyzer.TryMatchLoop(loop, out var operation, out var accumulatorName, out _))
        {
            return Task.CompletedTask;
        }

        CodeFixRegistration.Register(
            context,
            context.Diagnostics[0],
            "Use TensorPrimitives",
            ManualSpanSumAnalyzer.DiagnosticId,
            ct => ApplyFixAsync(context.Document, context.Diagnostics[0].Location.SourceSpan, operation, accumulatorName, ct));

        return Task.CompletedTask;
    }

    private static async Task<Document> ApplyFixAsync(
        Document document,
        TextSpan span,
        SpanOperation operation,
        string accumulatorName,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var loop = root?.FindNode(span, getInnermostNodeForTie: true)
            .AncestorsAndSelf()
            .OfType<ForEachStatementSyntax>()
            .FirstOrDefault();

        if (root is null || loop is null)
        {
            return document;
        }

        var call = "TensorPrimitives." + MemberName(operation) + "(" + loop.Expression.WithoutTrivia().ToFullString() + ")";

        if (operation is not SpanOperation.Sum
            && loop.Parent is BlockSyntax block
            && FindDeclaration(block, loop, accumulatorName) is { } declarator
            && ReplaceInitializer(root, block, loop, declarator, call) is { } replacedRoot)
        {
            return document.WithSyntaxRoot(replacedRoot);
        }

        var replacement = SyntaxFactory.ParseStatement(
            accumulatorName + (operation is SpanOperation.Sum ? " += " : " = ") + call + ";");

        return await FixSyntax.ReplaceLoopAsync(
            document,
            loop,
            replacement,
            TensorsNamespace,
            cancellationToken).ConfigureAwait(false);
    }

    private static SyntaxNode? ReplaceInitializer(
        SyntaxNode root,
        BlockSyntax block,
        ForEachStatementSyntax loop,
        VariableDeclaratorSyntax declarator,
        string call)
    {
        if (declarator.Initializer is null
            || declarator.Parent?.Parent is not LocalDeclarationStatementSyntax declarationStatement)
        {
            return null;
        }

        var initializer = SyntaxFactory.ParseExpression(call).WithTriviaFrom(declarator.Initializer.Value);
        var updatedStatement = declarationStatement.ReplaceNode(declarator.Initializer.Value, initializer);

        var statements = new List<StatementSyntax>(block.Statements.Count);
        foreach (var statement in block.Statements)
        {
            if (statement.Span == loop.Span)
            {
                continue;
            }

            statements.Add(statement.Span == declarationStatement.Span ? updatedStatement : statement);
        }

        if (statements.Count == block.Statements.Count)
        {
            return null;
        }

        var newBlock = block.WithStatements(SyntaxFactory.List(statements));
        var newRoot = root.ReplaceNode(block, newBlock);

        return UsingDirectives.AddIfMissing(newRoot, TensorsNamespace);
    }

    private static VariableDeclaratorSyntax? FindDeclaration(
        BlockSyntax block,
        ForEachStatementSyntax loop,
        string accumulatorName)
    {
        foreach (var statement in block.Statements)
        {
            if (statement.SpanStart >= loop.SpanStart)
            {
                break;
            }

            if (statement is LocalDeclarationStatementSyntax declaration)
            {
                var declarator = declaration.Declaration.Variables
                    .FirstOrDefault(variable => string.Equals(variable.Identifier.ValueText, accumulatorName, StringComparison.Ordinal));

                if (declarator is not null)
                {
                    return declarator;
                }
            }
        }

        return null;
    }

    private static string MemberName(SpanOperation operation)
    {
        if (operation is SpanOperation.Max)
        {
            return "Max";
        }

        return operation is SpanOperation.Min ? "Min" : "Sum";
    }
}

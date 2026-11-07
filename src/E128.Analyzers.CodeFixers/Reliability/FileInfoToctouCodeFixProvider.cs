using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Reliability;

/// <summary>
///     Code fix for E128056: wraps the flagged file-read call's containing statement
///     in a <c lang="csharp">try { } catch (System.IO.IOException) { return default; }</c> block.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(FileInfoToctouCodeFixProvider))]
[Shared]
public sealed class FileInfoToctouCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [FileInfoToctouAnalyzer.DiagnosticId];

    protected override async Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);
        var statement = node.FirstAncestorOrSelf<StatementSyntax>();
        if (statement is null)
        {
            return;
        }

        var semanticModel = await context.Document
            .GetSemanticModelAsync(context.CancellationToken)
            .ConfigureAwait(false);
        if (semanticModel?.GetEnclosingSymbol(statement.SpanStart, context.CancellationToken)
            is not IMethodSymbol enclosing)
        {
            return;
        }

        // The catch body returns default, so this rewrite fits a return statement. Any other
        // statement in a value-returning method would leave the try block, so its local would
        // lose its scope, and the catch would have nothing to return.
        var isReturn = statement is ReturnStatementSyntax;

        if (!isReturn && !enclosing.ReturnsVoid)
        {
            return;
        }

        var catchBody = isReturn
            ? SyntaxFactory.Block(
                SyntaxFactory.ReturnStatement(
                    SyntaxFactory.LiteralExpression(SyntaxKind.DefaultLiteralExpression)))
            : SyntaxFactory.Block();

        CodeFixRegistration.Register(context, diagnostic, "Wrap file read in try/catch(IOException)", nameof(FileInfoToctouCodeFixProvider), ct => FixSyntax.WrapInTryCatchAsync(context.Document, statement, "System.IO.IOException", catchBody, ct));
    }
}

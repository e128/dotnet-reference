using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Reliability;

/// <summary>
///     Code fix for E128057: wraps the flagged cleanup call's containing statement
///     in a <c lang="csharp">try { } catch (System.Exception) { }</c> block within the finally clause.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(FinallyBlockCleanupCodeFixProvider))]
[Shared]
public sealed class FinallyBlockCleanupCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [FinallyBlockCleanupAnalyzer.DiagnosticId];

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);
        var statement = node.FirstAncestorOrSelf<StatementSyntax>();
        if (statement is null)
        {
            return Task.CompletedTask;
        }

        // A local declared by the statement would leave the try block. A yield inside a try block
        // with a catch clause does not compile either.
        if (statement is LocalDeclarationStatementSyntax or YieldStatementSyntax)
        {
            return Task.CompletedTask;
        }

        CodeFixRegistration.Register(context, diagnostic, "Wrap cleanup in try/catch(Exception)", nameof(FinallyBlockCleanupCodeFixProvider), ct => FixSyntax.WrapInTryCatchAsync(context.Document, statement, "System.Exception", SyntaxFactory.Block(), ct));

        return Task.CompletedTask;
    }
}

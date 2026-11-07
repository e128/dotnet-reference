using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Performance;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(LoggerArgumentGuardCodeFixProvider))]
[Shared]
public sealed class LoggerArgumentGuardCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [LoggerArgumentGuardAnalyzer.DiagnosticId];

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        if (node is not InvocationExpressionSyntax
            {
                Parent: ExpressionStatementSyntax { Parent: BlockSyntax } statement,
                Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: var methodName } memberAccess,
            })
        {
            return Task.CompletedTask;
        }

        if (!LoggerArgumentGuardAnalyzer.TryGetLevel(methodName, out var level))
        {
            return Task.CompletedTask;
        }

        // The receiver appears twice in the fixed code, so it must be free of calls.
        if (memberAccess.Expression.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().Any())
        {
            return Task.CompletedTask;
        }

        var indent = FixSyntax.LastIndentation(statement.GetLeadingTrivia());

        var receiver = memberAccess.Expression.ToString();
        var body = statement.ToString();
        var text = "if ("
            + receiver
            + ".IsEnabled(global::Microsoft.Extensions.Logging.LogLevel."
            + level
            + "))\n"
            + indent
            + "{\n"
            + indent
            + "    "
            + body
            + "\n"
            + indent
            + "}";

        CodeFixRegistration.Register(context, diagnostic, "Guard the log call with IsEnabled", nameof(LoggerArgumentGuardCodeFixProvider), _ => Task.FromResult(FixSyntax.ReplaceStatementFromText(context.Document, root, statement, text)));

        return Task.CompletedTask;
    }
}

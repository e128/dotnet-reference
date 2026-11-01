using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Unsafe;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SkipInitCodeFixProvider))]
[Shared]
public sealed class SkipInitCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [SkipInitAnalyzer.DiagnosticId];

    public override FixAllProvider? GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    protected override async Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        if (node is not InvocationExpressionSyntax { Parent: ExpressionStatementSyntax statement } invocation
            || invocation.ArgumentList.Arguments.Count != 1)
        {
            return;
        }

        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        var typeName = model?.GetSymbolInfo(invocation, context.CancellationToken).Symbol is IMethodSymbol { TypeArguments.Length: 1 } skipInit
            ? skipInit.TypeArguments[0].ToMinimalDisplayString(model, invocation.SpanStart)
            : null;
        var text = BuildReplacement(invocation.ArgumentList.Arguments[0].Expression, typeName);
        if (text is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Initialize the local",
                _ => Task.FromResult(ApplyFix(context.Document, root, statement, text)),
                nameof(SkipInitCodeFixProvider)),
            diagnostic);
    }

    private static string? BuildReplacement(ExpressionSyntax argument, string? inferredTypeName)
    {
        if (argument is DeclarationExpressionSyntax { Designation: SingleVariableDesignationSyntax designation } declaration)
        {
            // `var x = default;` does not compile (CS0815), so spell out the type argument.
            var typeText = declaration.Type.IsVar && inferredTypeName is not null
                ? inferredTypeName
                : declaration.Type.ToString();
            var name = designation.Identifier.ValueText;

            return typeText + " " + name + " = default;";
        }

        return argument is IdentifierNameSyntax identifier
            ? identifier.Identifier.ValueText + " = default;"
            : null;
    }

    private static Document ApplyFix(
        Document document,
        SyntaxNode root,
        ExpressionStatementSyntax statement,
        string text)
    {
        var replacement = SyntaxFactory.ParseStatement(text).WithTriviaFrom(statement);
        return document.WithSyntaxRoot(root.ReplaceNode(statement, replacement));
    }
}

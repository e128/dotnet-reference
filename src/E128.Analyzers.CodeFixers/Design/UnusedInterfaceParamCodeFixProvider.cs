using System;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using E128.Analyzers.Style;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;

namespace E128.Analyzers.Design;

/// <summary>
///     Code fix for E128059: renames the unused parameter to <c lang="csharp">_</c> or prefixes it with
///     an underscore (<c lang="csharp">_paramName</c>) to signal intentional discard.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UnusedInterfaceParamCodeFixProvider))]
[Shared]
public sealed class UnusedInterfaceParamCodeFixProvider : CodeFixProvider
{
    // A rename touches the whole solution, so the batch fixer loses one of two edits in the same
    // document. Apply the renames in sequence instead.
    private static readonly SequentialRenameFixAllProvider s_fixAllProvider =
        new((_, symbolName) => symbolName is null || symbolName.StartsWith("_", StringComparison.Ordinal)
                ? null
                : "_" + symbolName,
            nameof(UnusedInterfaceParamCodeFixProvider));

    public override ImmutableArray<string> FixableDiagnosticIds =>
        [UnusedInterfaceParamAnalyzer.DiagnosticId];

    public override FixAllProvider GetFixAllProvider()
    {
        return s_fixAllProvider;
    }

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);
        if (node is not ParameterSyntax param)
        {
            return;
        }

        var paramName = param.Identifier.ValueText;
        if (paramName.StartsWith("_", StringComparison.Ordinal))
        {
            // The fix-all path skips an underscore-prefixed name, so a single fix must skip it too
            // rather than offer a rename to the same text.
            return;
        }

        var newName = $"_{paramName}";

        context.RegisterCodeFix(
            CodeAction.Create(
                $"Rename to '{newName}'",
                ct => RenameParameterAsync(context.Document, param, newName, ct),
                nameof(UnusedInterfaceParamCodeFixProvider)),
            diagnostic);
    }

    private static async Task<Solution> RenameParameterAsync(
        Document document,
        ParameterSyntax param,
        string newName,
        CancellationToken cancellationToken)
    {
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return document.Project.Solution;
        }

        var symbol = semanticModel.GetDeclaredSymbol(param, cancellationToken);
        return symbol is null
            ? document.Project.Solution
            : await Renamer.RenameSymbolAsync(
                document.Project.Solution,
                symbol,
                new SymbolRenameOptions(),
                newName,
                cancellationToken).ConfigureAwait(false);
    }
}

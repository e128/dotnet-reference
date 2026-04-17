using System;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;

namespace E128.Analyzers.Design;

/// <summary>
///     Code fix for E128059: renames the unused parameter to <c>_</c> or prefixes it with
///     an underscore (<c>_paramName</c>) to signal intentional discard.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UnusedInterfaceParamCodeFixProvider))]
[Shared]
public sealed class UnusedInterfaceParamCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [UnusedInterfaceParamAnalyzer.DiagnosticId];

    public override FixAllProvider GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
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
        var newName = paramName.StartsWith("_", StringComparison.Ordinal)
            ? paramName
            : $"_{paramName}";

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

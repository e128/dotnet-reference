using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;

namespace E128.Analyzers.Style;

/// <summary>
///     Applies renames one at a time to the evolving solution so each rename sees the result of
///     the previous one. <see cref="WellKnownFixAllProviders.BatchFixer" /> computes all changes
///     from the original snapshot and then merges — that merge fails when multiple
///     <see cref="Renamer" /> rename calls touch the same document.
/// </summary>
internal sealed class SequentialRenameFixAllProvider : FixAllProvider
{
    private readonly Func<Diagnostic, string?, string?> _computeNewName;
    private readonly string _providerName;

    internal SequentialRenameFixAllProvider(
        Func<Diagnostic, string?, string?> computeNewName,
        string providerName)
    {
        _computeNewName = computeNewName;
        _providerName = providerName;
    }

    public override async Task<CodeAction?> GetFixAsync(FixAllContext fixAllContext)
    {
        var renames = await CollectRenamesAsync(fixAllContext).ConfigureAwait(false);
        if (renames.Count == 0)
        {
            return null;
        }

        var title = renames.Count == 1
            ? $"Rename '{renames[0].OldName}' to '{renames[0].NewName}'"
            : $"Fix {renames.Count.ToString(CultureInfo.InvariantCulture)} naming style violations";

        return CodeAction.Create(
            title,
            ct => ApplyRenamesSequentiallyAsync(fixAllContext.Project.Solution, renames, ct),
            _providerName);
    }

    private async Task<List<RenameInfo>> CollectRenamesAsync(FixAllContext context)
    {
        var result = new List<RenameInfo>();

        foreach (var (document, diagnostics) in await GetDocumentDiagnosticsAsync(context).ConfigureAwait(false))
        {
            var root = await document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var semanticModel = await document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            if (root is null || semanticModel is null)
            {
                continue;
            }

            foreach (var diagnostic in diagnostics)
            {
                var node = root.FindNode(diagnostic.Location.SourceSpan);
                var symbol = semanticModel.GetDeclaredSymbol(node, context.CancellationToken)
                             ?? semanticModel.GetSymbolInfo(node, context.CancellationToken).Symbol;

                if (symbol is null)
                {
                    continue;
                }

                var newName = _computeNewName(diagnostic, symbol.Name);
                if (newName is null || string.Equals(symbol.Name, newName, StringComparison.Ordinal))
                {
                    continue;
                }

                result.Add(new RenameInfo(document.Id, diagnostic.Location.SourceSpan, symbol.Name, newName));
            }
        }

        // Process back-to-front within each document so renames at higher positions
        // don't shift the spans of symbols that appear earlier in the same file.
        return [.. result.OrderBy(r => r.DocumentId.Id).ThenByDescending(r => r.Span.Start)];
    }

    private static async Task<List<(Document Document, ImmutableArray<Diagnostic> Diagnostics)>>
        GetDocumentDiagnosticsAsync(FixAllContext context)
    {
        var result = new List<(Document, ImmutableArray<Diagnostic>)>();

        if (context.Scope == FixAllScope.Document && context.Document is not null)
        {
            var diagnostics = await context.GetDocumentDiagnosticsAsync(context.Document).ConfigureAwait(false);
            if (diagnostics.Length > 0)
            {
                result.Add((context.Document, diagnostics));
            }

            return result;
        }

        var projects = context.Scope == FixAllScope.Solution
            ? context.Project.Solution.Projects
            : [context.Project];

        foreach (var project in projects)
        {
            foreach (var document in project.Documents)
            {
                var diagnostics = await context.GetDocumentDiagnosticsAsync(document).ConfigureAwait(false);
                if (diagnostics.Length > 0)
                {
                    result.Add((document, diagnostics));
                }
            }
        }

        return result;
    }

    private static async Task<Solution> ApplyRenamesSequentiallyAsync(
        Solution solution,
        List<RenameInfo> renames,
        CancellationToken ct)
    {
        foreach (var rename in renames)
        {
            var document = solution.GetDocument(rename.DocumentId);
            if (document is null)
            {
                continue;
            }

            var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
            var semanticModel = await document.GetSemanticModelAsync(ct).ConfigureAwait(false);
            if (root is null || semanticModel is null || rename.Span.End > root.FullSpan.End)
            {
                continue;
            }

            var node = root.FindNode(rename.Span);
            var symbol = semanticModel.GetDeclaredSymbol(node, ct)
                         ?? semanticModel.GetSymbolInfo(node, ct).Symbol;

            // Skip if the symbol was already renamed by a previous fix (name no longer matches).
            if (symbol is null || !string.Equals(symbol.Name, rename.OldName, StringComparison.Ordinal))
            {
                continue;
            }

            solution = await Renamer.RenameSymbolAsync(
                    solution, symbol, new SymbolRenameOptions(), rename.NewName, ct)
                .ConfigureAwait(false);
        }

        return solution;
    }

    private readonly struct RenameInfo
    {
        public DocumentId DocumentId { get; }
        public TextSpan Span { get; }
        public string OldName { get; }
        public string NewName { get; }

        public RenameInfo(DocumentId documentId, TextSpan span, string oldName, string newName)
        {
            DocumentId = documentId;
            Span = span;
            OldName = oldName;
            NewName = newName;
        }
    }
}

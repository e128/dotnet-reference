using System;
using System.Collections.Immutable;
using System.Composition;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Rename;

namespace E128.Analyzers.Style;

/// <summary>
///     Code fix for E128063: renames private static members with mid-name underscores by removing
///     the underscore and PascalCasing the segments (e.g., <c>Nots_supported</c> → <c>NotsSupported</c>).
///     Uses Renamer.RenameSymbolAsync for safe project-wide renaming.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MidNameUnderscoreCodeFixProvider))]
[Shared]
public sealed class MidNameUnderscoreCodeFixProvider : CodeFixProvider
{
    private static readonly SequentialRenameFixAllProvider s_fixAllProvider =
        new((_, symbolName) => symbolName is null ? null : ComputeFixedName(symbolName),
            nameof(MidNameUnderscoreCodeFixProvider));

    public override ImmutableArray<string> FixableDiagnosticIds =>
        [MidNameUnderscoreAnalyzer.DiagnosticId];

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

        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return;
        }

        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        var symbol = semanticModel.GetDeclaredSymbol(node, context.CancellationToken);
        if (symbol is null)
        {
            return;
        }

        var newName = ComputeFixedName(symbol.Name);
        if (string.Equals(symbol.Name, newName, StringComparison.Ordinal))
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                $"Rename to '{newName}'",
                ct => Renamer.RenameSymbolAsync(
                    context.Document.Project.Solution, symbol, new SymbolRenameOptions(), newName, ct),
                nameof(MidNameUnderscoreCodeFixProvider)),
            diagnostic);
    }

    /// <summary>
    ///     Removes mid-name underscores and PascalCases each segment.
    ///     Preserves legitimate prefixes: <c>s_foo_bar</c> → <c>s_fooBar</c> (only the mid-name
    ///     underscore after the prefix is fixed), <c>Nots_supported</c> → <c>NotsSupported</c>.
    /// </summary>
    internal static string ComputeFixedName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        // Determine the prefix boundary (0 for no prefix, 2 for s_/m_/t_ prefixes, 1 for _ prefix)
        var prefixLength = GetPrefixLength(name);
        var result = new StringBuilder(name.Length);
        result.Append(name, 0, prefixLength);

        var body = name.Substring(prefixLength);

        // Split the body on underscores and PascalCase each segment
        var segments = body.Split(['_'], StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length <= 1)
        {
            // No mid-name underscores in the body — nothing to fix
            return name;
        }

        // Preserve first segment's casing, PascalCase subsequent segments
        for (var i = 0; i < segments.Length; i++)
        {
            if (i == 0)
            {
                result.Append(segments[i]);
            }
            else
            {
                result.Append(char.ToUpperInvariant(segments[i][0]))
                    .Append(segments[i], 1, segments[i].Length - 1);
            }
        }

        return result.ToString();
    }

    private static int GetPrefixLength(string name)
    {
        // s_foo, m_foo, t_foo — 2-char prefix
        if (name.Length >= 2
            && name[1] == '_'
            && IsHungarianPrefixChar(name[0]))
        {
            return 2;
        }

        // _foo — 1-char prefix
        return name.Length > 0 && name[0] == '_' ? 1 : 0;
    }

    private static bool IsHungarianPrefixChar(char c)
    {
        return c is 's' or 'm' or 't';
    }
}

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Rename;

namespace E128.Analyzers.Style;

/// <summary>
///     Code fix for IDE1006 (naming rule violation): renames the symbol to the compliant name
///     derived from the diagnostic's embedded naming style properties.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(NamingStyleCodeFixProvider))]
[Shared]
public sealed class NamingStyleCodeFixProvider : CodeFixProvider
{
    // Roslyn embeds these keys in IDE1006 diagnostic properties.
    private const string SuggestedNameKey = "SuggestedName"; // pre-computed by Roslyn's naming analyzer
    private const string SymbolNameKey = "SymbolName"; // only present in FakeNamingViolationAnalyzer (tests)
    private const string PrefixKey = "Prefix";
    private const string SuffixKey = "Suffix";
    private const string WordSeparatorKey = "WordSeparator";
    private const string CapitalizationSchemeKey = "CapitalizationScheme";

    private static readonly ImmutableArray<string> s_knownPrefixes = ["s_", "m_", "_", "I", "T"];

    private static readonly SequentialRenameFixAllProvider s_fixAllProvider =
        new(ComputeCompliantName, nameof(NamingStyleCodeFixProvider));

    public override ImmutableArray<string> FixableDiagnosticIds => ["IDE1006"];

    public override FixAllProvider? GetFixAllProvider()
    {
        return s_fixAllProvider;
    }

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics[0];

        // Resolve the symbol first so its name can serve as a fallback when the diagnostic
        // does not embed "SymbolName" (real Roslyn IDE1006 diagnostics omit it).
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

        var node = root.FindNode(diagnostic.Location.SourceSpan);
        var symbol = semanticModel.GetDeclaredSymbol(node, context.CancellationToken)
                     ?? semanticModel.GetSymbolInfo(node, context.CancellationToken).Symbol;

        if (symbol is null)
        {
            return;
        }

        var suggestedName = ComputeCompliantName(diagnostic, symbol.Name);
        if (suggestedName is null)
        {
            return;
        }

        if (string.Equals(symbol.Name, suggestedName, StringComparison.Ordinal))
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                $"Rename to '{suggestedName}'",
                ct => RenameSymbolAsync(
                    context.Document.Project.Solution, symbol, suggestedName, ct),
                nameof(NamingStyleCodeFixProvider)),
            diagnostic);
    }

    private static Task<Solution> RenameSymbolAsync(
        Solution solution,
        ISymbol symbol,
        string newName,
        CancellationToken cancellationToken)
    {
        return Renamer.RenameSymbolAsync(
            solution, symbol, new SymbolRenameOptions(), newName, cancellationToken);
    }

    // Single-arg overload: used by tests (FakeNamingViolationAnalyzer embeds SymbolName in properties).
    internal static string? ComputeCompliantName(Diagnostic diagnostic)
    {
        return ComputeCompliantName(diagnostic, null);
    }

    // Two-arg overload: used by RegisterCodeFixesAsync with the actual resolved symbol name.
    // When the diagnostic was produced by Roslyn's built-in naming analyzer it embeds
    // "SuggestedName" (pre-computed) but not "SymbolName", so symbolNameFallback is required.
    internal static string? ComputeCompliantName(Diagnostic diagnostic, string? symbolNameFallback)
    {
        var props = diagnostic.Properties;

        // Roslyn's IDE1006 diagnostic embeds the already-computed compliant name directly.
        if (props.TryGetValue(SuggestedNameKey, out var precomputedName) && !string.IsNullOrEmpty(precomputedName))
        {
            return precomputedName;
        }

        // Fall back to the provided symbol name (production) or read it from properties (tests).
        var symbolName = symbolNameFallback;
        if (string.IsNullOrEmpty(symbolName) &&
            (!props.TryGetValue(SymbolNameKey, out symbolName) || string.IsNullOrEmpty(symbolName)))
        {
            return null;
        }

        props.TryGetValue(PrefixKey, out var prefix);
        props.TryGetValue(SuffixKey, out var suffix);
        props.TryGetValue(WordSeparatorKey, out var wordSeparator);
        props.TryGetValue(CapitalizationSchemeKey, out var capitalizationScheme);

        prefix ??= string.Empty;
        suffix ??= string.Empty;
        wordSeparator ??= string.Empty;
        capitalizationScheme ??= "PascalCase";

        return symbolName is not string nonNullName
            ? null
            : BuildCompliantName(nonNullName, prefix, suffix, wordSeparator, capitalizationScheme);
    }

    internal static string BuildCompliantName(
        string symbolName,
        string prefix,
        string suffix,
        string wordSeparator,
        string capitalizationScheme)
    {
        // Strip any existing prefix so we work with the base identifier.
        // Common prefixes that may already be present but don't match the required one.
        var baseName = StripExistingAffixes(symbolName, prefix, suffix);

        // Split the base name into words: if the separator appears in the name, use it;
        // otherwise fall back to camelCase / PascalCase transition splitting.
        var words = !string.IsNullOrEmpty(wordSeparator) && baseName.Contains(wordSeparator, StringComparison.Ordinal)
            ? baseName.Split([wordSeparator], StringSplitOptions.RemoveEmptyEntries)
            : SplitByCaseTransition(baseName);

        if (words.Length == 0)
        {
            words = [baseName];
        }

        var fixedBase = ApplyCapitalization(words, wordSeparator, capitalizationScheme);
        return prefix + fixedBase + suffix;
    }

    private static string StripExistingAffixes(string name, string requiredPrefix, string requiredSuffix)
    {
        var result = name;

        // Strip suffix first (so prefix stripping works on the trimmed name).
        if (!string.IsNullOrEmpty(requiredSuffix) && result.EndsWith(requiredSuffix, StringComparison.Ordinal))
        {
            result = result.Substring(0, result.Length - requiredSuffix.Length);
        }

        // If the name already starts with the required prefix, strip it before re-applying below.
        if (!string.IsNullOrEmpty(requiredPrefix) && result.StartsWith(requiredPrefix, StringComparison.Ordinal))
        {
            return result.Substring(requiredPrefix.Length);
        }

        // For single-char letter prefixes, strip a wrong-case variant when followed by an uppercase
        // character — this signals the prefix was intended but cased incorrectly (e.g., "iFetcher"
        // when prefix is "I"). Without this, capitalization later produces "IFetcher" and then the
        // prefix is prepended again to yield "IIFetcher".
        if (!string.IsNullOrEmpty(requiredPrefix)
            && requiredPrefix.Length == 1
            && char.IsLetter(requiredPrefix[0])
            && result.Length > 1
            && char.ToUpperInvariant(result[0]) == char.ToUpperInvariant(requiredPrefix[0])
            && result[0] != requiredPrefix[0]
            && char.IsUpper(result[1]))
        {
            return result.Substring(1);
        }

        // Strip any common wrong prefix so we get the bare identifier words.
        foreach (var known in s_knownPrefixes)
        {
            if (string.Equals(known, requiredPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            if (result.StartsWith(known, StringComparison.Ordinal))
            {
                var afterKnown = result.Substring(known.Length);

                // Single-char letter prefixes ("I" for interfaces, "T" for type params) only
                // count as a prefix when the next character is uppercase. "IndexFilenames" starts
                // with "I" but the "I" is part of the word — not a naming prefix.
                if (known.Length == 1 && char.IsLetter(known[0]) &&
                    (afterKnown.Length == 0 || !char.IsUpper(afterKnown[0])))
                {
                    continue;
                }

                result = afterKnown;
                break;
            }
        }

        return result;
    }

    internal static string[] SplitByCaseTransition(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return [];
        }

        // Walk the string and split at lower→upper transitions.
        var words = new List<string>((name.Length >> 2) + 1);
        var wordStart = 0;

        for (var i = 1; i < name.Length; i++)
        {
            var prev = name[i - 1];
            var curr = name[i];

            // Split when transitioning from lowercase to uppercase: "myField" → "my|Field"
            if (char.IsLower(prev) && char.IsUpper(curr))
            {
                words.Add(name.Substring(wordStart, i - wordStart));
                wordStart = i;
            }
            // Split when transitioning from multiple uppercase to uppercase+lowercase: "ABCDef" → "ABC|Def"
            else if (i >= 2 && char.IsUpper(prev) && char.IsLower(curr) && char.IsUpper(name[i - 2]))
            {
                var segment = name.Substring(wordStart, i - 1 - wordStart);
                if (!string.IsNullOrEmpty(segment))
                {
                    words.Add(segment);
                }

                wordStart = i - 1;
            }
        }

        words.Add(name.Substring(wordStart));
        return [.. words];
    }

    private static string ApplyCapitalization(string[] words, string wordSeparator, string scheme)
    {
        return words.Length == 0
            ? string.Empty
            : scheme switch
            {
                "PascalCase" => ApplyPascalCase(words, wordSeparator),
                "CamelCase" => ApplyCamelCase(words, wordSeparator),
                "AllUpperCase" or "AllUpper" => ApplyAllUpper(words, wordSeparator),
                "AllLowerCase" or "AllLower" => ApplyAllLower(words, wordSeparator),
                "FirstUpper" => ApplyFirstUpper(words, wordSeparator),
                _ => ApplyPascalCase(words, wordSeparator)
            };
    }

    private static string ApplyPascalCase(string[] words, string separator)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < words.Length; i++)
        {
            if (i > 0 && !string.IsNullOrEmpty(separator))
            {
                builder.Append(separator);
            }

            AppendCapitalized(builder, words[i]);
        }

        return builder.ToString();
    }

    private static string ApplyCamelCase(string[] words, string separator)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < words.Length; i++)
        {
            if (i > 0 && !string.IsNullOrEmpty(separator))
            {
                builder.Append(separator);
            }

            if (i == 0)
            {
                AppendLowered(builder, words[i]);
            }
            else
            {
                AppendCapitalized(builder, words[i]);
            }
        }

        return builder.ToString();
    }

    private static string ApplyAllUpper(string[] words, string separator)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < words.Length; i++)
        {
            if (i > 0 && !string.IsNullOrEmpty(separator))
            {
                builder.Append(separator);
            }

            builder.Append(words[i].ToUpperInvariant());
        }

        return builder.ToString();
    }

    private static string ApplyAllLower(string[] words, string separator)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < words.Length; i++)
        {
            if (i > 0 && !string.IsNullOrEmpty(separator))
            {
                builder.Append(separator);
            }

            foreach (var c in words[i])
            {
                builder.Append(char.ToLowerInvariant(c));
            }
        }

        return builder.ToString();
    }

    private static string ApplyFirstUpper(string[] words, string separator)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < words.Length; i++)
        {
            if (i > 0 && !string.IsNullOrEmpty(separator))
            {
                builder.Append(separator);
            }

            if (i == 0)
            {
                AppendCapitalized(builder, words[i]);
            }
            else
            {
                builder.Append(words[i]);
            }
        }

        return builder.ToString();
    }

    private static void AppendCapitalized(StringBuilder builder, string word)
    {
        if (string.IsNullOrEmpty(word))
        {
            return;
        }

        builder.Append(char.ToUpperInvariant(word[0]));
        if (word.Length > 1)
        {
            builder.Append(word, 1, word.Length - 1);
        }
    }

    private static void AppendLowered(StringBuilder builder, string word)
    {
        if (string.IsNullOrEmpty(word))
        {
            return;
        }

        builder.Append(char.ToLowerInvariant(word[0]));
        if (word.Length > 1)
        {
            builder.Append(word, 1, word.Length - 1);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Rename;

namespace E128.Analyzers.Style;

/// <summary>
///     Code fix for IDE1006 (naming rule violation): renames the symbol to the compliant name
///     derived from the diagnostic's embedded naming style properties.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(NamingStyleCodeFixProvider))]
[Shared]
public sealed class NamingStyleCodeFixProvider : E128CodeFixProviderBase
{
    private const string SuggestedNameKey = "SuggestedName"; // pre-computed by Roslyn's naming analyzer
    private const string SymbolNameKey = "SymbolName"; // only present in FakeNamingViolationAnalyzer (tests)
    private const string PrefixKey = "Prefix";
    private const string SuffixKey = "Suffix";
    private const string WordSeparatorKey = "WordSeparator";
    private const string CapitalizationSchemeKey = "CapitalizationScheme";

    private const int AcronymLookback = 2;

    private static readonly ImmutableArray<string> s_knownPrefixes = ["s_", "m_", "_", "I", "T"];

    private static readonly SequentialRenameFixAllProvider s_fixAllProvider =
        new(ComputeCompliantName, nameof(NamingStyleCodeFixProvider));

    public override ImmutableArray<string> FixableDiagnosticIds => ["IDE1006"];

    public override FixAllProvider? GetFixAllProvider()
    {
        return s_fixAllProvider;
    }

    protected override async Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];

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

        CodeFixRegistration.Register(context, diagnostic, $"Rename to '{suggestedName}'", nameof(NamingStyleCodeFixProvider), ct => RenameSymbolAsync(
                    context.Document.Project.Solution, symbol, suggestedName, ct));
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

    internal static string? ComputeCompliantName(Diagnostic diagnostic)
    {
        return ComputeCompliantName(diagnostic, null);
    }

    internal static string? ComputeCompliantName(Diagnostic diagnostic, string? symbolNameFallback)
    {
        var props = diagnostic.Properties;

        if (props.TryGetValue(SuggestedNameKey, out var precomputedName) && !string.IsNullOrEmpty(precomputedName))
        {
            return precomputedName;
        }

        var symbolName = ResolveSymbolName(props, symbolNameFallback);
        if (symbolName is null)
        {
            return null;
        }

        _ = props.TryGetValue(PrefixKey, out var prefix);
        _ = props.TryGetValue(SuffixKey, out var suffix);
        _ = props.TryGetValue(WordSeparatorKey, out var wordSeparator);
        _ = props.TryGetValue(CapitalizationSchemeKey, out var capitalizationScheme);

        prefix ??= string.Empty;
        suffix ??= string.Empty;
        wordSeparator ??= string.Empty;
        capitalizationScheme ??= "PascalCase";

        return BuildCompliantName(symbolName, prefix, suffix, wordSeparator, capitalizationScheme);
    }

    private static string? ResolveSymbolName(ImmutableDictionary<string, string?> props, string? symbolNameFallback)
    {
        if (!string.IsNullOrEmpty(symbolNameFallback))
        {
            return symbolNameFallback;
        }

        return props.TryGetValue(SymbolNameKey, out var fromProperties) && !string.IsNullOrEmpty(fromProperties)
            ? fromProperties
            : null;
    }

    internal static string BuildCompliantName(
        string symbolName,
        string prefix,
        string suffix,
        string wordSeparator,
        string capitalizationScheme)
    {
        var baseName = StripExistingAffixes(symbolName, prefix, suffix);

        var words = !string.IsNullOrEmpty(wordSeparator) && baseName.Contains(wordSeparator, StringComparison.Ordinal)
            ? baseName.Split([wordSeparator], StringSplitOptions.RemoveEmptyEntries)
            : SplitByCaseTransition(baseName);

        if (words.Length is 0)
        {
            words = [baseName];
        }

        var fixedBase = ApplyCapitalization(words, wordSeparator, capitalizationScheme);
        return prefix + fixedBase + suffix;
    }

    private static string StripExistingAffixes(string name, string requiredPrefix, string requiredSuffix)
    {
        var result = StripSuffix(name, requiredSuffix);

        if (!string.IsNullOrEmpty(requiredPrefix) && result.StartsWith(requiredPrefix, StringComparison.Ordinal))
        {
            return result.Substring(requiredPrefix.Length);
        }

        if (IsWrongCaseLetterPrefix(result, requiredPrefix))
        {
            return result.Substring(1);
        }

        return StripKnownWrongPrefix(result, requiredPrefix);
    }

    private static string StripSuffix(string name, string requiredSuffix)
    {
        return !string.IsNullOrEmpty(requiredSuffix) && name.EndsWith(requiredSuffix, StringComparison.Ordinal)
            ? name.Substring(0, name.Length - requiredSuffix.Length)
            : name;
    }

    private static bool IsWrongCaseLetterPrefix(string name, string requiredPrefix)
    {
        return !string.IsNullOrEmpty(requiredPrefix)
               && requiredPrefix.Length is 1
               && char.IsLetter(requiredPrefix[0])
               && name.Length > 1
               && char.ToUpperInvariant(name[0]) == char.ToUpperInvariant(requiredPrefix[0])
               && name[0] != requiredPrefix[0]
               && char.IsUpper(name[1]);
    }

    private static string StripKnownWrongPrefix(string name, string requiredPrefix)
    {
        foreach (var known in s_knownPrefixes)
        {
            if (string.Equals(known, requiredPrefix, StringComparison.Ordinal)
                || !name.StartsWith(known, StringComparison.Ordinal))
            {
                continue;
            }

            var afterKnown = name.Substring(known.Length);
            if (IsLetterPrefixInsideWord(known, afterKnown))
            {
                continue;
            }

            return afterKnown;
        }

        return name;
    }

    private static bool IsLetterPrefixInsideWord(string known, string afterKnown)
    {
        return known.Length is 1
               && char.IsLetter(known[0])
               && (afterKnown.Length is 0 || !char.IsUpper(afterKnown[0]));
    }

    internal static string[] SplitByCaseTransition(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return [];
        }

        var words = new List<string>((name.Length >> 2) + 1);
        var wordStart = 0;

        for (var i = 1; i < name.Length; i++)
        {
            var prev = name[i - 1];
            var curr = name[i];

            if (char.IsLower(prev) && char.IsUpper(curr))
            {
                words.Add(name.Substring(wordStart, i - wordStart));
                wordStart = i;
            }
            else if (i >= AcronymLookback && char.IsUpper(prev) && char.IsLower(curr) && char.IsUpper(name[i - AcronymLookback]))
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
        return words.Length is 0
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
        return JoinWords(words, separator, static (builder, word, _) => AppendCapitalized(builder, word));
    }

    private static string ApplyCamelCase(string[] words, string separator)
    {
        return JoinWords(words, separator, static (builder, word, index) =>
        {
            if (index is 0)
            {
                AppendLowered(builder, word);
            }
            else
            {
                AppendCapitalized(builder, word);
            }
        });
    }

    private static string ApplyAllUpper(string[] words, string separator)
    {
        return JoinWords(words, separator, static (builder, word, _) => builder.Append(word.ToUpperInvariant()));
    }

    private static string ApplyAllLower(string[] words, string separator)
    {
        return JoinWords(words, separator, static (builder, word, _) =>
        {
            foreach (var c in word)
            {
                builder.Append(LowerInvariant(c));
            }
        });
    }

    private static string ApplyFirstUpper(string[] words, string separator)
    {
        return JoinWords(words, separator, static (builder, word, index) =>
        {
            if (index is 0)
            {
                AppendCapitalized(builder, word);
            }
            else
            {
                builder.Append(word);
            }
        });
    }

    private static string JoinWords(
        string[] words,
        string separator,
        Action<StringBuilder, string, int> append)
    {
        var builder = new StringBuilder();
        for (var index = 0; index < words.Length; index++)
        {
            if (index > 0 && !string.IsNullOrEmpty(separator))
            {
                builder.Append(separator);
            }

            append(builder, words[index], index);
        }

        return builder.ToString();
    }

    private static void AppendCapitalized(StringBuilder builder, string word)
    {
        if (string.IsNullOrEmpty(word))
        {
            return;
        }

        builder.Append(char.ToUpperInvariant(word[0])).Append(word, 1, word.Length - 1);
    }

    private static void AppendLowered(StringBuilder builder, string word)
    {
        if (string.IsNullOrEmpty(word))
        {
            return;
        }

        builder.Append(LowerInvariant(word[0])).Append(word, 1, word.Length - 1);
    }

    private static char LowerInvariant(char c)
    {
        return CultureInfo.InvariantCulture.TextInfo.ToLower(c);
    }
}

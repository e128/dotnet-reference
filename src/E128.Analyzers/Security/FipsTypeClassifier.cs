using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace E128.Analyzers.Security;

/// <summary>
///     Classifies a cryptography type for E128071 and E128148. One type gets one verdict, so the two rules never
///     report the same type. The verdict is cached per compilation.
/// </summary>
internal static class FipsTypeClassifier
{
    /// <summary>
    ///     Static members that produce an instance without <see langword="new" />, such as
    ///     <c lang="csharp">MD5.Create()</c> and the .NET 5 and later one-shot helpers
    ///     (<c lang="csharp">MD5.HashData</c>). Both type rules inspect the declaring type of these members.
    /// </summary>
    public static readonly ImmutableHashSet<string> OneShotMembers = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "Create",
        "HashData",
        "TryHashData",
        "HashDataAsync");

    /// <summary>
    ///     Fragments that name a cipher on their own. IDEA is also an ordinary word, so it needs a cryptographic
    ///     context before it counts (see <see cref="HasCipherContext" />).
    /// </summary>
    private static readonly ImmutableArray<string> ForbiddenNameFragments = ImmutableArray.Create(
        "Blowfish",
        "RC4",
        "Arcfour",
        "CAST5",
        "CAST6",
        "IDEA",
        "Skipjack");

    /// <summary>
    ///     Words that mark a type as a cipher implementation when they directly follow an ambiguous fragment and end
    ///     the type name, as in <c lang="csharp">IdeaEngine</c>. <c lang="csharp">IdeaRankingAlgorithm</c> and
    ///     <c lang="csharp">IdeaManagedService</c> do not match.
    /// </summary>
    private static readonly ImmutableArray<string> CipherRoleWords = ImmutableArray.Create(
        "Engine",
        "Cipher",
        "Cng",
        "Managed",
        "CryptoServiceProvider",
        "Algorithm",
        "Transform",
        "Encryptor",
        "Decryptor",
        "Cbc",
        "Ecb");

    /// <summary>Returns the cached verdict for <paramref name="type" /> in <paramref name="compilation" />.</summary>
    public static TypeVerdict GetVerdict(Compilation compilation, ITypeSymbol type)
    {
        var cache = TypeVerdictCache.For(compilation);

        if (!cache.TryGetValue(type, out var verdict))
        {
            verdict = Classify(type);
            cache.TryAdd(type, verdict);
        }

        return verdict;
    }

    private static TypeVerdict Classify(ITypeSymbol type)
    {
        // A framework type outside System.Security.Cryptography is never a forbidden algorithm, so skip the
        // string scans.
        if (FipsSymbolHelpers.IsBclType(type) && !FipsSymbolHelpers.IsInCryptographyNamespaceTree(type))
        {
            return TypeVerdict.Allowed;
        }

        // The type itself, then each base type: a type that derives from a forbidden type inherits the algorithm.
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (FipsSymbolHelpers.IsCryptographyType(current, FipsForbiddenTypes.All))
            {
                return TypeVerdict.ForbiddenType;
            }
        }

        foreach (var fragment in ForbiddenNameFragments)
        {
            if (FipsSymbolHelpers.ContainsWord(type.Name, fragment) &&
                (!string.Equals(fragment, "IDEA", StringComparison.Ordinal) || HasCipherContext(type, fragment)))
            {
                return TypeVerdict.ForbiddenName;
            }
        }

        return TypeVerdict.Allowed;
    }

    /// <summary>
    ///     Decides whether a type whose name contains an ambiguous fragment (such as <c lang="csharp">Idea</c>) is a
    ///     cipher. A cipher has a role word directly after the fragment that ends the name
    ///     (<c lang="csharp">IdeaEngine</c>), derives from a cipher or symmetric algorithm base, implements a cipher
    ///     interface, or is named exactly like the cipher inside a cryptographic namespace.
    /// </summary>
    private static bool HasCipherContext(ITypeSymbol type, string fragment)
    {
        foreach (var role in CipherRoleWords)
        {
            if (type.Name.EndsWith(fragment + role, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        for (var baseType = type.BaseType; baseType is not null; baseType = baseType.BaseType)
        {
            if (baseType.Name.IndexOf("Cipher", StringComparison.OrdinalIgnoreCase) >= 0 ||
                FipsSymbolHelpers.IsCryptographyType(baseType, "SymmetricAlgorithm"))
            {
                return true;
            }
        }

        foreach (var contract in type.AllInterfaces)
        {
            if (contract.Name.IndexOf("Cipher", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        if (!string.Equals(type.Name, fragment, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        for (var ns = type.ContainingNamespace; ns is not null && !ns.IsGlobalNamespace; ns = ns.ContainingNamespace)
        {
            if (ns.Name.IndexOf("crypt", StringComparison.OrdinalIgnoreCase) >= 0 ||
                ns.Name.IndexOf("cipher", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }
}

using System;
using System.Collections.Immutable;

namespace E128.Analyzers.Security;

/// <summary>
///     The System.Security.Cryptography type names that E128071 reports. E128071, E128148 and
///     <see cref="FipsTypeClassifier" /> read this set, so no analyzer depends on another analyzer's members.
/// </summary>
internal static class FipsForbiddenTypes
{
    /// <summary>Types whose use the policy blocks outright.</summary>
    public static readonly ImmutableHashSet<string> Algorithms = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "MD5",
        "MD5CryptoServiceProvider",
        "MD5Managed",
        "MD5Cng",
        "SHA1",
        "SHA1CryptoServiceProvider",
        "SHA1Managed",
        "SHA1Cng",
        "RIPEMD160",
        "RIPEMD160Managed",
        "DES",
        "DESCryptoServiceProvider",
        "RC2",
        "RC2CryptoServiceProvider",
        "TripleDES",
        "TripleDESCryptoServiceProvider",
        "TripleDESCng",
        "HMACMD5",
        "HMACSHA1",
        "HMACRIPEMD160",
        "MACTripleDES");

    /// <summary>
    ///     Types that organizational policy excludes without a claim that every use is prohibited by NIST.
    ///     <c lang="csharp">ChaCha20Poly1305</c> is not in the approved algorithm list.
    ///     <c lang="csharp">PasswordDeriveBytes</c> implements the obsolete PBKDF1 and defaults to SHA-1.
    /// </summary>
    public static readonly ImmutableHashSet<string> Excluded = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "ChaCha20Poly1305",
        "PasswordDeriveBytes");

    /// <summary>Every type E128071 reports.</summary>
    public static readonly ImmutableHashSet<string> All =
        Algorithms.Union(Excluded).WithComparer(StringComparer.Ordinal);
}

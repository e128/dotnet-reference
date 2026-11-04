using System.Linq;
using Microsoft.CodeAnalysis;

namespace E128.Analyzers;

/// <summary>
///     Decides whether an assembly ships with the framework. The public key token is the proof. An
///     assembly name is not, because any project can name its assembly System.Foo.
/// </summary>
internal static class FrameworkAssemblyIdentity
{
    /// <summary>
    ///     Public key tokens of framework assemblies: ECMA (b77a5c561934e089), Microsoft
    ///     (b03f5f7f11d50a3a), Microsoft extensions (adb9793829ddae60), Microsoft product
    ///     (31bf3856ad364e35), the core library (7cec85d7bea7798e), and netstandard
    ///     (cc7b13ffcd2ddd51).
    /// </summary>
    private static readonly byte[][] FrameworkPublicKeyTokens =
    [
        [0xb7, 0x7a, 0x5c, 0x56, 0x19, 0x34, 0xe0, 0x89],
        [0xb0, 0x3f, 0x5f, 0x7f, 0x11, 0xd5, 0x0a, 0x3a],
        [0xad, 0xb9, 0x79, 0x38, 0x29, 0xdd, 0xae, 0x60],
        [0x31, 0xbf, 0x38, 0x56, 0xad, 0x36, 0x4e, 0x35],
        [0x7c, 0xec, 0x85, 0xd7, 0xbe, 0xa7, 0x79, 0x8e],
        [0xcc, 0x7b, 0x13, 0xff, 0xcd, 0x2d, 0xdd, 0x51],
    ];

    /// <summary>
    ///     Returns true when the assembly carries a known framework public key token. An unsigned
    ///     assembly has no token and returns false.
    /// </summary>
    public static bool IsFrameworkAssembly(IAssemblySymbol? assembly)
    {
        if (assembly is null)
        {
            return false;
        }

        var token = assembly.Identity.PublicKeyToken;
        if (token.IsDefaultOrEmpty)
        {
            return false;
        }

        foreach (var known in FrameworkPublicKeyTokens)
        {
            if (token.SequenceEqual(known))
            {
                return true;
            }
        }

        return false;
    }
}

using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers;

internal static class InModifierHelper
{
    // The order the C# language reference gives for declaration modifiers. A token placed out of
    // order still compiles, so only a reader notices the defect.
    private static readonly SyntaxKind[] ModifierOrder =
    [
        SyntaxKind.PublicKeyword,
        SyntaxKind.PrivateKeyword,
        SyntaxKind.ProtectedKeyword,
        SyntaxKind.InternalKeyword,
        SyntaxKind.FileKeyword,
        SyntaxKind.StaticKeyword,
        SyntaxKind.ExternKeyword,
        SyntaxKind.NewKeyword,
        SyntaxKind.VirtualKeyword,
        SyntaxKind.AbstractKeyword,
        SyntaxKind.SealedKeyword,
        SyntaxKind.OverrideKeyword,
        SyntaxKind.ReadOnlyKeyword,
        SyntaxKind.UnsafeKeyword,
        SyntaxKind.RequiredKeyword,
        SyntaxKind.VolatileKeyword,
        SyntaxKind.AsyncKeyword,
        SyntaxKind.PartialKeyword
    ];

    /// <summary>
    ///     Adds a modifier token at the position the language reference defines. Returns the input
    ///     unchanged when the modifier is already present.
    /// </summary>
    internal static SyntaxTokenList AddModifier(SyntaxTokenList modifiers, SyntaxKind kind)
    {
        if (modifiers.Any(modifier => modifier.IsKind(kind)))
        {
            return modifiers;
        }

        var rank = RankOf(kind);
        var insertIndex = modifiers.Count;

        for (var i = 0; i < modifiers.Count; i++)
        {
            if (RankOf(modifiers[i].Kind()) > rank)
            {
                insertIndex = i;
                break;
            }
        }

        return modifiers.Insert(
            insertIndex,
            SyntaxFactory.Token(kind).WithTrailingTrivia(SyntaxFactory.Space));
    }

    private static int RankOf(SyntaxKind kind)
    {
        var rank = Array.IndexOf(ModifierOrder, kind);
        return rank < 0 ? ModifierOrder.Length : rank;
    }

    internal static bool HasInModifier(ParameterSyntax parameter)
    {
        foreach (var modifier in parameter.Modifiers)
        {
            if (modifier.IsKind(SyntaxKind.InKeyword))
            {
                return true;
            }
        }

        return false;
    }

    internal static ParameterSyntax RemoveInModifier(ParameterSyntax parameter)
    {
        var newModifiers = new SyntaxTokenList();
        foreach (var modifier in parameter.Modifiers)
        {
            if (!modifier.IsKind(SyntaxKind.InKeyword))
            {
                newModifiers = newModifiers.Add(modifier);
            }
        }

        var result = parameter.WithModifiers(newModifiers);

        if (!newModifiers.Any() && parameter.Type is not null)
        {
            var leadingTrivia = parameter.Modifiers[0].LeadingTrivia;
            result = result.WithType(parameter.Type.WithLeadingTrivia(leadingTrivia));
        }

        return result;
    }
}

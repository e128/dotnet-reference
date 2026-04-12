using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers;

internal static class InModifierHelper
{
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

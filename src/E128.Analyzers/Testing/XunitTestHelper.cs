using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Testing;

/// <summary>
///     Shared detection of xUnit <c lang="csharp">[Fact]</c> and <c lang="csharp">[Theory]</c> test methods. The
///     helper serves the low-value test analyzer. Adoption by the existing test-quality rules is deferred.
/// </summary>
internal static class XunitTestHelper
{
    internal static bool IsXunitTestMethod(IMethodSymbol method)
    {
        foreach (var attribute in method.GetAttributes())
        {
            if (IsFactOrTheoryAttribute(attribute))
            {
                return true;
            }
        }

        return false;
    }

    internal static AttributeSyntax? GetFactOrTheoryAttribute(MethodDeclarationSyntax method)
    {
        foreach (var attributeList in method.AttributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                if (IsFactOrTheoryName(SimpleName(attribute.Name)))
                {
                    return attribute;
                }
            }
        }

        return null;
    }

    internal static int CountDataRows(MethodDeclarationSyntax method)
    {
        var count = 0;

        foreach (var attributeList in method.AttributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                if (IsInlineDataName(SimpleName(attribute.Name)))
                {
                    count++;
                }
            }
        }

        return count;
    }

    // An attribute name can be written plain, qualified, or alias-qualified. Read the last segment
    // of the name so every spelling matches.
    private static string SimpleName(NameSyntax name)
    {
        return name switch
        {
            QualifiedNameSyntax qualified => SimpleName(qualified.Right),
            AliasQualifiedNameSyntax alias => SimpleName(alias.Name),
            _ => name.ToString()
        };
    }

    private static bool IsFactOrTheoryAttribute(AttributeData attribute)
    {
        return attribute.AttributeClass is { } attributeClass
               && (string.Equals(attributeClass.Name, "FactAttribute", StringComparison.Ordinal)
                   || string.Equals(attributeClass.Name, "TheoryAttribute", StringComparison.Ordinal))
               && IsXunitNamespace(attributeClass.ContainingNamespace);
    }

    private static bool IsFactOrTheoryName(string name)
    {
        return string.Equals(name, "Fact", StringComparison.Ordinal)
               || string.Equals(name, "Theory", StringComparison.Ordinal)
               || string.Equals(name, "FactAttribute", StringComparison.Ordinal)
               || string.Equals(name, "TheoryAttribute", StringComparison.Ordinal);
    }

    private static bool IsInlineDataName(string name)
    {
        return string.Equals(name, "InlineData", StringComparison.Ordinal)
               || string.Equals(name, "InlineDataAttribute", StringComparison.Ordinal);
    }

    private static bool IsXunitNamespace(INamespaceSymbol? ns)
    {
        return ns is not null && string.Equals(ns.ToDisplayString(), "Xunit", StringComparison.Ordinal);
    }
}

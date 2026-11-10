using System;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Testing;

/// <summary>
///     Shared detection of xUnit <c lang="csharp">[Fact]</c> and <c lang="csharp">[Theory]</c> test methods for every
///     test-quality rule. It also detects NUnit and MSTest test compilations.
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

    internal static bool IsFactOrTheoryAttribute(AttributeData attribute)
    {
        return attribute.AttributeClass is { } attributeClass && IsFactOrTheoryType(attributeClass);
    }

    internal static bool IsFactOrTheoryAttribute(
        AttributeSyntax attribute,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        return IsFactOrTheoryName(SimpleName(attribute.Name))
               && semanticModel.GetSymbolInfo(attribute, cancellationToken).Symbol is IMethodSymbol constructor
               && constructor.ContainingType is { } containingType
               && IsFactOrTheoryType(containingType);
    }

    /// <summary>
    ///     True when the compilation references xUnit, NUnit, or MSTest and declares at least one test
    ///     method. A reference alone does not exempt the whole compilation, because a production
    ///     project can share an assembly with a test framework. <c lang="csharp">GetTypeByMetadataName</c>
    ///     returns null when two references declare the type, so the check reads every match.
    /// </summary>
    internal static bool IsTestCompilation(Compilation compilation, CancellationToken cancellationToken = default)
    {
        var hasFramework = compilation.GetTypesByMetadataName("Xunit.FactAttribute").Length > 0
                           || compilation.GetTypesByMetadataName("NUnit.Framework.TestAttribute").Length > 0
                           || compilation.GetTypesByMetadataName("Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute").Length > 0;

        if (!hasFramework)
        {
            return false;
        }

        foreach (var tree in compilation.SyntaxTrees)
        {
            foreach (var method in tree.GetRoot(cancellationToken).DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                if (HasTestAttribute(method))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsOtherFrameworkTestName(string name)
    {
        return string.Equals(name, "Test", StringComparison.Ordinal)
               || string.Equals(name, "TestCase", StringComparison.Ordinal)
               || string.Equals(name, "TestMethod", StringComparison.Ordinal)
               || string.Equals(name, "DataTestMethod", StringComparison.Ordinal);
    }

    private static bool HasTestAttribute(MethodDeclarationSyntax method)
    {
        if (GetFactOrTheoryAttribute(method) is not null)
        {
            return true;
        }

        foreach (var attributeList in method.AttributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                if (IsOtherFrameworkTestName(SimpleName(attribute.Name)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    internal static bool IsXunitNamespace(INamespaceSymbol? ns)
    {
        return ns is not null && string.Equals(ns.ToDisplayString(), "Xunit", StringComparison.Ordinal);
    }

    internal static bool IsAssertType(INamedTypeSymbol? type)
    {
        return type is not null
               && string.Equals(type.Name, "Assert", StringComparison.Ordinal)
               && IsXunitNamespace(type.ContainingNamespace);
    }

    // An attribute name can be written plain, qualified, or alias-qualified. Read the last segment
    // of the name so every spelling matches.
    internal static string SimpleName(NameSyntax name)
    {
        return name switch
        {
            QualifiedNameSyntax qualified => SimpleName(qualified.Right),
            AliasQualifiedNameSyntax alias => SimpleName(alias.Name),
            _ => name.ToString()
        };
    }

    private static bool IsFactOrTheoryType(INamedTypeSymbol attributeClass)
    {
        return (string.Equals(attributeClass.Name, "FactAttribute", StringComparison.Ordinal)
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
}

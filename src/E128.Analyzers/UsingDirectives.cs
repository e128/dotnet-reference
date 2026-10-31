using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers;

internal static class UsingDirectives
{
    /// <summary>
    ///     Adds a top-level using directive when the file does not already have it. The new directive
    ///     goes into ordinal position, so the file stays sorted without a later format pass.
    /// </summary>
    internal static SyntaxNode AddIfMissing(SyntaxNode root, string namespaceName)
    {
        if (root is not CompilationUnitSyntax compilationUnit)
        {
            return root;
        }

        if (compilationUnit.Usings.Any(usingDirective =>
                string.Equals(usingDirective.Name?.ToString(), namespaceName, StringComparison.Ordinal)))
        {
            return root;
        }

        var newUsing = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(namespaceName))
            .WithTrailingTrivia(SyntaxFactory.ElasticLineFeed);

        var insertIndex = 0;
        for (var i = 0; i < compilationUnit.Usings.Count; i++)
        {
            if (StringComparer.Ordinal.Compare(compilationUnit.Usings[i].Name?.ToString(), namespaceName) < 0)
            {
                insertIndex = i + 1;
            }
        }

        return compilationUnit.WithUsings(compilationUnit.Usings.Insert(insertIndex, newUsing));
    }
}

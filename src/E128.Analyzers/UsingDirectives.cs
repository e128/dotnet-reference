using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers;

internal static class UsingDirectives
{
    /// <summary>Adds a top-level using directive when the file does not already have it.</summary>
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

        var newUsing = SyntaxFactory
            .ParseCompilationUnit("using " + namespaceName + ";\n")
            .Usings[0]
            .WithTrailingTrivia(SyntaxFactory.LineFeed);

        return compilationUnit.AddUsings(newUsing);
    }
}

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
    ///     goes after the last directive that sorts before it, so an already-sorted list stays sorted.
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

        var usings = compilationUnit.Usings;
        var insertIndex = 0;
        for (var i = 0; i < usings.Count; i++)
        {
            if (StringComparer.Ordinal.Compare(usings[i].Name?.ToString(), namespaceName) < 0)
            {
                insertIndex = i + 1;
            }
        }

        // A file header, such as a licence comment, is leading trivia of the first node. Move it to
        // the new directive so the header stays at the top of the file, whether the header sat on
        // an existing using or on the first member of a file with no usings yet.
        if (insertIndex == 0)
        {
            if (usings.Count > 0)
            {
                newUsing = newUsing.WithLeadingTrivia(usings[0].GetLeadingTrivia());
                usings = usings.Replace(usings[0], usings[0].WithLeadingTrivia(SyntaxFactory.TriviaList()));
            }
            else if (compilationUnit.Members.FirstOrDefault() is { } firstMember
                     && firstMember.GetLeadingTrivia().Any())
            {
                newUsing = newUsing.WithLeadingTrivia(firstMember.GetLeadingTrivia());
                compilationUnit = compilationUnit.ReplaceNode(
                    firstMember, firstMember.WithLeadingTrivia(SyntaxFactory.TriviaList()));
            }
        }

        return compilationUnit.WithUsings(usings.Insert(insertIndex, newUsing));
    }
}

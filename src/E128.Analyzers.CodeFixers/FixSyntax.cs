using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers;

/// <summary>
///     Small syntax predicates and builders that more than one code fix provider needs.
/// </summary>
internal static class FixSyntax
{
    /// <summary>
    ///     True when any ancestor declaration is a static type, method, property, or field.
    /// </summary>
    internal static bool IsInStaticMember(SyntaxNode node)
    {
        return node.Ancestors().Any(ancestor => ancestor switch
        {
            BaseTypeDeclarationSyntax type => type.Modifiers.Any(SyntaxKind.StaticKeyword),
            BaseMethodDeclarationSyntax method => method.Modifiers.Any(SyntaxKind.StaticKeyword),
            BasePropertyDeclarationSyntax property => property.Modifiers.Any(SyntaxKind.StaticKeyword),
            BaseFieldDeclarationSyntax field => field.Modifiers.Any(SyntaxKind.StaticKeyword),
            _ => false
        });
    }

    /// <summary>
    ///     True when the argument is a lambda whose body is a parameterless object creation.
    /// </summary>
    internal static bool IsParameterlessCreation(ExpressionSyntax argument)
    {
        var body = argument switch
        {
            SimpleLambdaExpressionSyntax simple => simple.Body as ExpressionSyntax,
            ParenthesizedLambdaExpressionSyntax parenthesized => parenthesized.Body as ExpressionSyntax,
            _ => null
        };

        return body is ObjectCreationExpressionSyntax creation
               && (creation.ArgumentList?.Arguments.Count ?? 0) == 0;
    }

    /// <summary>
    ///     Returns the indentation of the member declaration, or four spaces when
    ///     the member carries no leading whitespace.
    /// </summary>
    internal static string MemberIndentation(MemberDeclarationSyntax member)
    {
        var indent = member.GetLeadingTrivia()
            .FirstOrDefault(trivia => trivia.IsKind(SyntaxKind.WhitespaceTrivia))
            .ToFullString();

        return indent.Length == 0 ? "    " : indent;
    }

    /// <summary>
    ///     Returns the member's trailing end-of-line trivia, or the standard newline.
    /// </summary>
    internal static string LineBreak(MemberDeclarationSyntax member)
    {
        foreach (var trivia in member.GetTrailingTrivia())
        {
            if (trivia.IsKind(SyntaxKind.EndOfLineTrivia))
            {
                return trivia.ToString();
            }
        }

        return "\n";
    }
}

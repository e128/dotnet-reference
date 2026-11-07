using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Style;

/// <summary>
///     Builds a pragma warning directive line. The style fixes share the token shape.
/// </summary>
internal static class PragmaDirectives
{
    /// <summary>
    ///     Returns a pragma warning directive for the keyword and error codes. Each token carries
    ///     its leading space and the line ends with a line feed.
    /// </summary>
    internal static PragmaWarningDirectiveTriviaSyntax Build(
        SyntaxKind keyword,
        SeparatedSyntaxList<ExpressionSyntax> errorCodes)
    {
        return SyntaxFactory.PragmaWarningDirectiveTrivia(
            SyntaxFactory.Token(SyntaxKind.HashToken),
            SyntaxFactory.Token(SyntaxKind.PragmaKeyword).WithLeadingTrivia(SyntaxFactory.Space),
            SyntaxFactory.Token(SyntaxKind.WarningKeyword).WithLeadingTrivia(SyntaxFactory.Space),
            SyntaxFactory.Token(keyword).WithLeadingTrivia(SyntaxFactory.Space),
            errorCodes,
            SyntaxFactory.Token(SyntaxKind.EndOfDirectiveToken).WithTrailingTrivia(SyntaxFactory.LineFeed),
            true);
    }
}

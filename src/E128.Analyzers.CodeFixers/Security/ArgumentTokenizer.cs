using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Security;

internal static class ArgumentTokenizer
{
    internal static List<ExpressionSyntax>? TryTokenize(ExpressionSyntax expression)
    {
        var parts = TryGetParts(expression);
        return parts is null ? null : SplitIntoTokens(parts);
    }

    private static List<StringPart>? TryGetParts(ExpressionSyntax expression)
    {
        return expression switch
        {
            LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression) =>
                [new StringPart(literal.Token.ValueText, null)],
            InterpolatedStringExpressionSyntax interpolated => TryGetInterpolatedParts(interpolated),
            _ => null
        };
    }

    private static List<StringPart>? TryGetInterpolatedParts(InterpolatedStringExpressionSyntax interpolated)
    {
        var parts = new List<StringPart>();

        foreach (var content in interpolated.Contents)
        {
            switch (content)
            {
                case InterpolatedStringTextSyntax text:
                    parts.Add(new StringPart(text.TextToken.ValueText, null));
                    break;
                case InterpolationSyntax { AlignmentClause: null, FormatClause: null } interpolation:
                    parts.Add(new StringPart(null, interpolation.Expression));
                    break;
                default:
                    return null;
            }
        }

        return parts;
    }

    private static List<ExpressionSyntax>? SplitIntoTokens(List<StringPart> parts)
    {
        var tokens = new List<ExpressionSyntax>();
        var currentAtoms = new List<StringPart>();
        var insideQuotes = false;

        foreach (var part in parts)
        {
            if (part.Hole is { } hole)
            {
                currentAtoms.Add(new StringPart(null, hole));
                continue;
            }

            AppendText(part.Text ?? string.Empty, currentAtoms, tokens, ref insideQuotes);
        }

        FinalizeToken(currentAtoms, tokens);

        return insideQuotes || tokens.Count is 0 ? null : tokens;
    }

    private static void AppendText(string text, List<StringPart> currentAtoms, List<ExpressionSyntax> tokens, ref bool insideQuotes)
    {
        var buffer = new StringBuilder();

        foreach (var c in text)
        {
            if (c == '"')
            {
                insideQuotes = !insideQuotes;
                continue;
            }

            if (!insideQuotes && char.IsWhiteSpace(c))
            {
                FlushBuffer(buffer, currentAtoms);
                FinalizeToken(currentAtoms, tokens);
                continue;
            }

            buffer.Append(c);
        }

        FlushBuffer(buffer, currentAtoms);
    }

    private static void FlushBuffer(StringBuilder buffer, List<StringPart> currentAtoms)
    {
        if (buffer.Length is 0)
        {
            return;
        }

        currentAtoms.Add(new StringPart(buffer.ToString(), null));
        buffer.Clear();
    }

    private static void FinalizeToken(List<StringPart> currentAtoms, List<ExpressionSyntax> tokens)
    {
        if (currentAtoms.Count is 0)
        {
            return;
        }

        tokens.Add(BuildTokenExpression(currentAtoms));
        currentAtoms.Clear();
    }

    private static ExpressionSyntax BuildTokenExpression(List<StringPart> atoms)
    {
        if (atoms.Count is 1 && atoms[0].Hole is { } singleHole)
        {
            return singleHole;
        }

        if (atoms.TrueForAll(static atom => atom.Hole is null))
        {
            var text = string.Concat(atoms.Select(static atom => atom.Text));
            return SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(text));
        }

        var contents = atoms.Select(BuildInterpolatedContent).ToArray();
        return SyntaxFactory.InterpolatedStringExpression(
            SyntaxFactory.Token(SyntaxKind.InterpolatedStringStartToken),
            SyntaxFactory.List(contents),
            SyntaxFactory.Token(SyntaxKind.InterpolatedStringEndToken));
    }

    private static InterpolatedStringContentSyntax BuildInterpolatedContent(StringPart atom)
    {
        if (atom.Hole is { } hole)
        {
            return SyntaxFactory.Interpolation(hole);
        }

        var text = atom.Text ?? string.Empty;
        return SyntaxFactory.InterpolatedStringText(
            SyntaxFactory.Token(default, SyntaxKind.InterpolatedStringTextToken, text, text, default));
    }

    private readonly struct StringPart : IEquatable<StringPart>
    {
        private const int HashMultiplier = 31;

        internal StringPart(string? text, ExpressionSyntax? hole)
        {
            Text = text;
            Hole = hole;
        }

        internal string? Text { get; }

        internal ExpressionSyntax? Hole { get; }

        public static bool operator ==(StringPart left, StringPart right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(StringPart left, StringPart right)
        {
            return !left.Equals(right);
        }

        public bool Equals(StringPart other)
        {
            return string.Equals(Text, other.Text, StringComparison.Ordinal)
                   && ReferenceEquals(Hole, other.Hole);
        }

        public override bool Equals([NotNullWhen(true)] object? obj)
        {
            return obj is StringPart other && Equals(other);
        }

        public override int GetHashCode()
        {
            var textHash = Text is null ? 0 : StringComparer.Ordinal.GetHashCode(Text);
            var holeHash = Hole is null ? 0 : RuntimeHelpers.GetHashCode(Hole);

            return unchecked((textHash * HashMultiplier) + holeHash);
        }

        public override string ToString()
        {
            return Text ?? Hole?.ToString() ?? string.Empty;
        }
    }
}

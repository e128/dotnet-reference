using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Performance;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MultiStringEqualsOrChainCodeFixProvider))]
[Shared]
public sealed class MultiStringEqualsOrChainCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [MultiStringEqualsOrChainAnalyzer.DiagnosticId];

    public override FixAllProvider? GetFixAllProvider()
    {
        // Two chains on the same operand in one type both need the field name, and a batch merge
        // emits that field twice. The fix stays single-diagnostic.
        return null;
    }

    protected override async Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        var orExpr = node.AncestorsAndSelf()
            .OfType<BinaryExpressionSyntax>()
            .FirstOrDefault(b => b.IsKind(SyntaxKind.LogicalOrExpression));
        if (orExpr is null)
        {
            return;
        }

        var typeDecl = orExpr.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();
        if (typeDecl is null)
        {
            return;
        }

        // Pre-validate so we only register a fix that will actually change code.
        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return;
        }

        if (!TryExtractChainInfo(semanticModel, orExpr, context.CancellationToken, out var pivot, out var literals))
        {
            return;
        }

        var fieldName = BuildFieldName(pivot.OperandText);
        if (HasConflictingField(typeDecl, fieldName))
        {
            return;
        }

        // The only no-op path is a failed field parse. Validate that too, so a registered fix
        // always changes code.
        var fieldDecl = ParseFieldDeclaration(fieldName, GetComparerExpression(pivot.ComparisonKey), literals);
        if (fieldDecl is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Replace OR-chain with HashSet<string>.Contains()",
                ct => ApplyFixAsync(context.Document, orExpr, typeDecl, ct),
                nameof(MultiStringEqualsOrChainCodeFixProvider)),
            diagnostic);
    }

    private static async Task<Document> ApplyFixAsync(
        Document document,
        BinaryExpressionSyntax orExpr,
        TypeDeclarationSyntax typeDecl,
        CancellationToken cancellationToken)
    {
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return document;
        }

        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        if (!TryExtractChainInfo(semanticModel, orExpr, cancellationToken,
                out var pivot, out var literals))
        {
            return document;
        }

        var fieldName = BuildFieldName(pivot.OperandText);
        if (HasConflictingField(typeDecl, fieldName))
        {
            return document;
        }

        var newRoot = BuildFixedRoot(root, typeDecl, orExpr, pivot, literals, fieldName);
        return newRoot is null ? document : document.WithSyntaxRoot(newRoot);
    }

    private static bool TryExtractChainInfo(
        SemanticModel semanticModel,
        BinaryExpressionSyntax orExpr,
        CancellationToken cancellationToken,
        out MultiStringEqualsOrChainAnalyzer.StringEqualityInfo pivot,
        out List<string> literals)
    {
        pivot = default;
        literals = [];

        var operands = MultiStringEqualsOrChainAnalyzer.FlattenOrChain(orExpr);
        var infos = new List<MultiStringEqualsOrChainAnalyzer.StringEqualityInfo>(operands.Count);
        foreach (var op in operands)
        {
            var info = MultiStringEqualsOrChainAnalyzer.TryExtractStringEquality(semanticModel, op, cancellationToken);
            if (info is not null)
            {
                infos.Add(info.Value);
            }
        }

        // Only fix fully homogeneous chains: all leaves qualify and form one group.
        if (infos.Count < MultiStringEqualsOrChainAnalyzer.MinChainLength
            || infos.Count != operands.Count)
        {
            return false;
        }

        pivot = infos[0];
        foreach (var info in infos)
        {
            if (!string.Equals(info.OperandText, pivot.OperandText, StringComparison.Ordinal)
                || !string.Equals(info.ComparisonKey, pivot.ComparisonKey, StringComparison.Ordinal))
            {
                return false; // Heterogeneous chain — no fix.
            }
        }

        foreach (var info in infos)
        {
            literals.Add(info.Literal);
        }

        return true;
    }

    private static SyntaxNode? BuildFixedRoot(
        SyntaxNode root,
        TypeDeclarationSyntax typeDecl,
        BinaryExpressionSyntax orExpr,
        in MultiStringEqualsOrChainAnalyzer.StringEqualityInfo pivot,
        List<string> literals,
        string fieldName)
    {
        var comparerExpr = GetComparerExpression(pivot.ComparisonKey);
        var fieldDecl = ParseFieldDeclaration(fieldName, comparerExpr, literals);
        if (fieldDecl is null)
        {
            return null;
        }

        // Preserve the OR-chain's leading trivia (indentation) on the replacement expression.
        var containsExpr = SyntaxFactory
            .ParseExpression($"{fieldName}.Contains({pivot.OperandText})")
            .WithTriviaFrom(orExpr);

        // Replace OR-chain, then insert field as first member with matching indentation.
        var typeDeclAfterReplace = typeDecl.ReplaceNode(orExpr, containsExpr);
        var typeDeclWithField = InsertFieldAsFirstMember(typeDeclAfterReplace, fieldDecl);

        var newRoot = root.ReplaceNode(typeDecl, typeDeclWithField);
        newRoot = UsingDirectives.AddIfMissing(newRoot, "System");
        return UsingDirectives.AddIfMissing(newRoot, "System.Collections.Generic");
    }

    private static TypeDeclarationSyntax InsertFieldAsFirstMember(
        TypeDeclarationSyntax typeDecl,
        MemberDeclarationSyntax fieldDecl)
    {
        if (!typeDecl.Members.Any())
        {
            return typeDecl.WithMembers(typeDecl.Members.Add(fieldDecl));
        }

        // Detect indent string from the existing first member's leading whitespace.
        var firstMember = typeDecl.Members[0];
        var firstMemberLeadingTrivia = firstMember.GetLeadingTrivia();
        var fieldWithIndent = fieldDecl
            .WithLeadingTrivia(SyntaxFactory.Whitespace(FixSyntax.MemberIndentation(firstMember)));

        // Give the existing first member a blank line before it, and keep its own leading trivia so
        // a doc comment stays with the member.
        var firstMemberWithBlankLine = firstMember.WithLeadingTrivia(
            firstMemberLeadingTrivia.InsertRange(
                0,
                SyntaxFactory.TriviaList(SyntaxFactory.LineFeed, SyntaxFactory.LineFeed)));

        var updatedMembers = typeDecl.Members
            .Replace(firstMember, firstMemberWithBlankLine)
            .Insert(0, fieldWithIndent);

        return typeDecl.WithMembers(updatedMembers);
    }

    // A fallback member writes broken code into the document, so abort the fix when the parse fails.
    private static MemberDeclarationSyntax? ParseFieldDeclaration(
        string fieldName,
        string comparerExpr,
        List<string> literals)
    {
        var sb = new StringBuilder()
            .Append("private static readonly HashSet<string> ")
            .Append(fieldName)
            .Append(" = new(")
            .Append(comparerExpr)
            .Append(") { ");
        for (var i = 0; i < literals.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }

            // The analyzer captures Token.ValueText, which is already unescaped. FormatLiteral puts
            // the escapes back so a literal with a quote or a backslash stays valid C#.
            sb.Append(SymbolDisplay.FormatLiteral(literals[i], quote: true));
        }

        sb.Append(" };");
        return SyntaxFactory.ParseMemberDeclaration(sb.ToString());
    }

    private static bool HasConflictingField(TypeDeclarationSyntax typeDecl, string fieldName)
    {
        foreach (var member in typeDecl.Members.OfType<FieldDeclarationSyntax>())
        {
            foreach (var variable in member.Declaration.Variables)
            {
                if (string.Equals(variable.Identifier.ValueText, fieldName, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    internal static string BuildFieldName(string operandText)
    {
        var lastDot = operandText.LastIndexOf('.');
        var simpleName = lastDot >= 0 ? operandText.Substring(lastDot + 1) : operandText;

        var sb = new StringBuilder();
        foreach (var c in simpleName)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                sb.Append(c);
            }
        }

        var identifier = sb.Length > 0 ? sb.ToString() : "value";
        var lowerFirst = char.ToLowerInvariant(identifier[0]).ToString();
        if (identifier.Length > 1)
        {
            lowerFirst += identifier.Substring(1);
        }

        return "_" + lowerFirst + "Values";
    }

    private static string GetComparerExpression(string comparisonKey)
    {
        if (string.Equals(comparisonKey, "==", StringComparison.Ordinal))
        {
            return "StringComparer.Ordinal";
        }

        // StringComparison.OrdinalIgnoreCase -> StringComparer.OrdinalIgnoreCase
        const string comparisonPrefix = "StringComparison.";
        const string comparerPrefix = "StringComparer.";
        return comparisonKey.StartsWith(comparisonPrefix, StringComparison.Ordinal)
            ? comparerPrefix + comparisonKey.Substring(comparisonPrefix.Length)
            : "StringComparer.Ordinal";
    }
}

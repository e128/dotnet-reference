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
public sealed class MultiStringEqualsOrChainCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [MultiStringEqualsOrChainAnalyzer.DiagnosticId];

    public override FixAllProvider? GetFixAllProvider() => null;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

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

        if (!TryExtractChainInfo(semanticModel, orExpr, context.CancellationToken, out var pivot, out _))
        {
            return;
        }

        if (HasConflictingField(typeDecl, BuildFieldName(pivot.OperandText)))
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Replace OR-chain with HashSet<string>.Contains()",
                createChangedDocument: ct => ApplyFixAsync(context.Document, orExpr, typeDecl, ct),
                equivalenceKey: nameof(MultiStringEqualsOrChainCodeFixProvider)),
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
        return document.WithSyntaxRoot(newRoot);
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

    private static SyntaxNode BuildFixedRoot(
        SyntaxNode root,
        TypeDeclarationSyntax typeDecl,
        BinaryExpressionSyntax orExpr,
        in MultiStringEqualsOrChainAnalyzer.StringEqualityInfo pivot,
        List<string> literals,
        string fieldName)
    {
        var comparerExpr = GetComparerExpression(pivot.ComparisonKey);
        var fieldDecl = ParseFieldDeclaration(fieldName, comparerExpr, literals);

        // Preserve the OR-chain's leading trivia (indentation) on the replacement expression.
        var containsExpr = SyntaxFactory
            .ParseExpression($"{fieldName}.Contains({pivot.OperandText})")
            .WithTriviaFrom(orExpr);

        // Replace OR-chain, then insert field as first member with matching indentation.
        var typeDeclAfterReplace = typeDecl.ReplaceNode(orExpr, containsExpr);
        var typeDeclWithField = InsertFieldAsFirstMember(typeDeclAfterReplace, fieldDecl);

        var newRoot = root.ReplaceNode(typeDecl, typeDeclWithField);
        newRoot = AddUsingIfMissing(newRoot, "System");
        return AddUsingIfMissing(newRoot, "System.Collections.Generic");
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
        var indentStr = firstMember.GetLeadingTrivia()
            .FirstOrDefault(t => t.IsKind(SyntaxKind.WhitespaceTrivia))
            .ToFullString();
        if (indentStr.Length == 0)
        {
            indentStr = "    ";
        }

        var fieldWithIndent = fieldDecl.WithLeadingTrivia(SyntaxFactory.Whitespace(indentStr));

        // Give the existing first member a blank line before it: \n\n<indent>
        // (ParseMemberDeclaration produces no trailing newline on ";", so we need two linefeeds.)
        var firstMemberWithBlankLine = firstMember.WithLeadingTrivia(
            SyntaxFactory.TriviaList(
                SyntaxFactory.LineFeed,
                SyntaxFactory.LineFeed,
                SyntaxFactory.Whitespace(indentStr)));

        var updatedMembers = typeDecl.Members
            .Replace(firstMember, firstMemberWithBlankLine)
            .Insert(0, fieldWithIndent);

        return typeDecl.WithMembers(updatedMembers);
    }

    private static SyntaxNode AddUsingIfMissing(SyntaxNode root, string namespaceName)
    {
        if (root is not CompilationUnitSyntax compilationUnit)
        {
            return root;
        }

        foreach (var u in compilationUnit.Usings)
        {
            if (string.Equals(u.Name?.ToString(), namespaceName, StringComparison.Ordinal))
            {
                return root;
            }
        }

        var newUsing = SyntaxFactory
            .ParseCompilationUnit("using " + namespaceName + ";\n")
            .Usings[0];
        return compilationUnit.AddUsings(newUsing);
    }

    private static MemberDeclarationSyntax ParseFieldDeclaration(
        string fieldName, string comparerExpr, List<string> literals)
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

            sb.Append('"').Append(literals[i]).Append('"');
        }

        sb.Append(" };");
        var parsed = SyntaxFactory.ParseMemberDeclaration(sb.ToString());
        return parsed ?? SyntaxFactory.FieldDeclaration(
            SyntaxFactory.VariableDeclaration(SyntaxFactory.IdentifierName("var")));
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
        var lowerFirst = char.ToLowerInvariant(identifier[0])
            + (identifier.Length > 1 ? identifier.Substring(1) : string.Empty);
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

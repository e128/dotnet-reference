using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Performance;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SearchValuesHoistingCodeFixProvider))]
[Shared]
public sealed class SearchValuesHoistingCodeFixProvider : CodeFixProvider
{
    private const string FallbackFieldName = "_searchValues";

    public override ImmutableArray<string> FixableDiagnosticIds =>
        [SearchValuesHoistingAnalyzer.DiagnosticId];

    public override FixAllProvider? GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        if (root is null)
        {
            return;
        }

        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);

        if (node is not InvocationExpressionSyntax invocation)
        {
            return;
        }

        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (model?.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method)
        {
            return;
        }

        var typeDeclaration = invocation.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();
        if (typeDeclaration is null)
        {
            return;
        }

        var baseName = GetBaseName(invocation);
        var fieldName = Enumerable.Range(1, int.MaxValue - 1)
            .Select(number => number == 1
                ? baseName
                : baseName + number.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .First(candidate => !HasMember(typeDeclaration, candidate));

        var fieldType = method.ReturnType.ToDisplayString();

        context.RegisterCodeFix(
            CodeAction.Create(
                "Cache in a static readonly field",
                _ => Task.FromResult(
                    ApplyFix(context.Document, root, invocation, typeDeclaration, fieldType, fieldName)),
                nameof(SearchValuesHoistingCodeFixProvider)),
            diagnostic);
    }

    // Name the field after the method that used the table, for example `_containsDelimiterSearchValues`.
    private static string GetBaseName(SyntaxNode invocation)
    {
        var method = invocation.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (method is null || method.Identifier.ValueText.Length == 0)
        {
            return FallbackFieldName;
        }

        var name = method.Identifier.ValueText;
        return "_" + char.ToLowerInvariant(name[0]) + name.Substring(1) + "SearchValues";
    }

    private static Document ApplyFix(
        Document document,
        SyntaxNode root,
        InvocationExpressionSyntax invocation,
        TypeDeclarationSyntax typeDeclaration,
        string fieldType,
        string fieldName)
    {
        var field = SyntaxFactory.FieldDeclaration(
            SyntaxFactory.VariableDeclaration(SyntaxFactory.ParseTypeName(fieldType))
                .WithVariables(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.VariableDeclarator(fieldName)
                            .WithInitializer(
                                SyntaxFactory.EqualsValueClause(invocation.WithoutTrivia())))))
            .WithModifiers(
                SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
                    SyntaxFactory.Token(SyntaxKind.StaticKeyword),
                    SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword)));

        var replacement = SyntaxFactory
            .IdentifierName(fieldName)
            .WithTriviaFrom(invocation);

        var typeWithField = InsertFieldAsFirstMember(
            typeDeclaration.ReplaceNode(invocation, replacement),
            field);

        return document.WithSyntaxRoot(root.ReplaceNode(typeDeclaration, typeWithField));
    }

    private static TypeDeclarationSyntax InsertFieldAsFirstMember(
        TypeDeclarationSyntax typeDeclaration,
        MemberDeclarationSyntax field)
    {
        if (typeDeclaration.Members.Count == 0)
        {
            return typeDeclaration.WithMembers(typeDeclaration.Members.Add(field));
        }

        var firstMember = typeDeclaration.Members[0];
        var indent = firstMember.GetLeadingTrivia()
            .FirstOrDefault(trivia => trivia.IsKind(SyntaxKind.WhitespaceTrivia))
            .ToFullString();

        if (indent.Length == 0)
        {
            indent = "    ";
        }

        // Keep the existing trivia, so a doc comment on the first member survives. Skip the
        // line break that ends the open brace line. Set both ends of the gap explicitly, so the
        // result has one blank line whether the synthesized field keeps elastic trivia or not.
        var leading = firstMember.GetLeadingTrivia();
        var index = 0;
        var lineBreak = "\n";

        for (; index < leading.Count && leading[index].IsKind(SyntaxKind.EndOfLineTrivia); index++)
        {
            lineBreak = leading[index].ToString();
        }

        var fieldWithIndent = field
            .WithLeadingTrivia(SyntaxFactory.Whitespace(indent))
            .WithTrailingTrivia(SyntaxFactory.EndOfLine(lineBreak));

        // Keep fields together and separate a field from other member kinds.
        var gap = firstMember is FieldDeclarationSyntax
            ? SyntaxFactory.TriviaList()
            : SyntaxFactory.TriviaList(SyntaxFactory.EndOfLine(lineBreak));

        var firstMemberWithBlankLine = firstMember.WithLeadingTrivia(gap.AddRange(leading.Skip(index)));

        var members = typeDeclaration.Members
            .Replace(firstMember, firstMemberWithBlankLine)
            .Insert(0, fieldWithIndent);

        return typeDeclaration.WithMembers(members);
    }

    private static bool HasMember(TypeDeclarationSyntax typeDeclaration, string name)
    {
        foreach (var member in typeDeclaration.Members)
        {
            if (member is FieldDeclarationSyntax field)
            {
                foreach (var variable in field.Declaration.Variables)
                {
                    if (string.Equals(variable.Identifier.ValueText, name, System.StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            if (member is PropertyDeclarationSyntax property
                && string.Equals(property.Identifier.ValueText, name, System.StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}

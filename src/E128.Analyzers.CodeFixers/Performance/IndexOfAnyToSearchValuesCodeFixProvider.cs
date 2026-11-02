using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Performance;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(IndexOfAnyToSearchValuesCodeFixProvider))]
[Shared]
public sealed class IndexOfAnyToSearchValuesCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [IndexOfAnyToSearchValuesAnalyzer.DiagnosticId];

    public override FixAllProvider? GetFixAllProvider()
    {
        return null;
    }

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        if (node is not InvocationExpressionSyntax invocation)
        {
            return Task.CompletedTask;
        }

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return Task.CompletedTask;
        }

        var receiver = memberAccess.Expression;
        if (receiver is null)
        {
            return Task.CompletedTask;
        }

        if (!TryExtractCharLiterals(invocation.ArgumentList.Arguments, out var chars))
        {
            return Task.CompletedTask;
        }

        var typeDecl = invocation.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();
        if (typeDecl is null)
        {
            return Task.CompletedTask;
        }

        var fieldName = BuildFieldName(receiver.ToString());
        if (HasConflictingMember(typeDecl, fieldName))
        {
            return Task.CompletedTask;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Use SearchValues<char> with a span IndexOfAny",
                _ => Task.FromResult(ApplyFix(
                    context.Document, root, typeDecl, invocation, receiver,
                    memberAccess.Name.Identifier.ValueText, fieldName, chars)),
                nameof(IndexOfAnyToSearchValuesCodeFixProvider)),
            diagnostic);

        return Task.CompletedTask;
    }

    private static Document ApplyFix(
        Document document,
        SyntaxNode root,
        TypeDeclarationSyntax typeDecl,
        InvocationExpressionSyntax invocation,
        ExpressionSyntax receiver,
        string methodName,
        string fieldName,
        string chars)
    {
        var fieldText = "private static readonly System.Buffers.SearchValues<char> " + fieldName
            + " = System.Buffers.SearchValues.Create(" + SymbolDisplay.FormatLiteral(chars, quote: true) + ");";
        var fieldDecl = SyntaxFactory.ParseMemberDeclaration(fieldText);
        if (fieldDecl is null)
        {
            return document;
        }

        // Preserve the invocation's leading trivia (indentation) on the replacement.
        var replacementText = receiver.ToFullString() + ".AsSpan()." + methodName + "(" + fieldName + ")";
        var replacement = SyntaxFactory
            .ParseExpression(replacementText)
            .WithTriviaFrom(invocation);

        var typeDeclAfterReplace = typeDecl.ReplaceNode(invocation, replacement);
        var typeDeclWithField = InsertFieldAsFirstMember(typeDeclAfterReplace, fieldDecl);

        var newRoot = root.ReplaceNode(typeDecl, typeDeclWithField);
        // The emitted field is fully qualified, but AsSpan() is an extension method on
        // System.MemoryExtensions, so the rewrite needs `using System;`.
        return document.WithSyntaxRoot(UsingDirectives.AddIfMissing(newRoot, "System"));
    }

    private static bool TryExtractCharLiterals(SeparatedSyntaxList<ArgumentSyntax> arguments, out string chars)
    {
        chars = string.Empty;
        if (arguments.Count != 1)
        {
            return false;
        }

        var expression = arguments[0].Expression;
        var initializer = expression switch
        {
            ArrayCreationExpressionSyntax array => array.Initializer,
            ImplicitArrayCreationExpressionSyntax implicitArray => implicitArray.Initializer,
            _ => null,
        };

        if (initializer is null)
        {
            return false;
        }

        var sb = new StringBuilder();
        foreach (var element in initializer.Expressions)
        {
            if (element is not LiteralExpressionSyntax literal
                || !literal.IsKind(SyntaxKind.CharacterLiteralExpression))
            {
                return false;
            }

            sb.Append(literal.Token.Value);
        }

        if (sb.Length == 0)
        {
            return false;
        }

        chars = sb.ToString();
        return true;
    }

    private static string BuildFieldName(string receiverText)
    {
        var lastDot = receiverText.LastIndexOf('.');
        var simpleName = lastDot >= 0 ? receiverText.Substring(lastDot + 1) : receiverText;

        var sb = new StringBuilder();
        foreach (var c in simpleName)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                sb.Append(c);
            }
        }

        var identifier = sb.Length > 0 ? sb.ToString() : "chars";
        var lowerFirst = char.ToLowerInvariant(identifier[0]).ToString();
        if (identifier.Length > 1)
        {
            lowerFirst += identifier.Substring(1);
        }

        return "_" + lowerFirst + "Chars";
    }

    private static TypeDeclarationSyntax InsertFieldAsFirstMember(
        TypeDeclarationSyntax typeDecl,
        MemberDeclarationSyntax fieldDecl)
    {
        if (!typeDecl.Members.Any())
        {
            return typeDecl.WithMembers(typeDecl.Members.Add(fieldDecl));
        }

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

    private static bool HasConflictingMember(TypeDeclarationSyntax typeDecl, string fieldName)
    {
        foreach (var member in typeDecl.Members)
        {
            if (member is FieldDeclarationSyntax field)
            {
                foreach (var variable in field.Declaration.Variables)
                {
                    if (string.Equals(variable.Identifier.ValueText, fieldName, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            if (member is PropertyDeclarationSyntax property
                && string.Equals(property.Identifier.ValueText, fieldName, StringComparison.Ordinal))
            {
                return true;
            }

            if (member is MethodDeclarationSyntax method
                && string.Equals(method.Identifier.ValueText, fieldName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}

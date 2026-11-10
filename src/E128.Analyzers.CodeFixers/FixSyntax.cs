using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
        return node.Ancestors().Any(static ancestor => ancestor switch
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
               && (creation.ArgumentList?.Arguments.Count ?? 0) is 0;
    }

    /// <summary>
    ///     Returns the indentation of the member declaration, or four spaces when
    ///     the member carries no leading whitespace.
    /// </summary>
    internal static string MemberIndentation(MemberDeclarationSyntax member)
    {
        var indent = member.GetLeadingTrivia()
            .FirstOrDefault(static trivia => trivia.IsKind(SyntaxKind.WhitespaceTrivia))
            .ToFullString();

        return indent.Length is 0 ? "    " : indent;
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

    /// <summary>
    ///     Inserts the field as the first member of the type, with the indentation of the member
    ///     that follows it. A blank line separates the two members.
    /// </summary>
    internal static TypeDeclarationSyntax InsertFieldAsFirstMember(
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
            .WithLeadingTrivia(SyntaxFactory.Whitespace(MemberIndentation(firstMember)));

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

    /// <summary>
    ///     Returns the last whitespace trivia in the list, or an empty string when none exists.
    /// </summary>
    internal static string LastIndentation(SyntaxTriviaList trivia)
    {
        var indent = string.Empty;
        foreach (var item in trivia)
        {
            if (item.IsKind(SyntaxKind.WhitespaceTrivia))
            {
                indent = item.ToFullString();
            }
        }

        return indent;
    }

    /// <summary>
    ///     Returns the list's last trivia when it is whitespace, or an empty list otherwise.
    /// </summary>
    internal static SyntaxTriviaList TrailingWhitespace(SyntaxTriviaList trivia)
    {
        return trivia.Count > 0 && trivia.Last().IsKind(SyntaxKind.WhitespaceTrivia)
            ? SyntaxFactory.TriviaList(trivia.Last())
            : SyntaxFactory.TriviaList();
    }

    /// <summary>
    ///     Wraps an expression in parentheses unless it is already a primary expression,
    ///     so appending an indexer or member access keeps the original meaning.
    /// </summary>
    internal static ExpressionSyntax ParenthesizeIfNeeded(ExpressionSyntax expression)
    {
        var bare = expression.WithoutTrivia();

        return bare is IdentifierNameSyntax
            or MemberAccessExpressionSyntax
            or InvocationExpressionSyntax
            or ElementAccessExpressionSyntax
            or ThisExpressionSyntax
            or ParenthesizedExpressionSyntax
            ? bare
            : SyntaxFactory.ParenthesizedExpression(bare);
    }

    /// <summary>
    ///     Returns an element access on the target, with the target parenthesized when needed.
    /// </summary>
    internal static ElementAccessExpressionSyntax IndexInto(ExpressionSyntax target, ExpressionSyntax index)
    {
        return SyntaxFactory.ElementAccessExpression(
            ParenthesizeIfNeeded(target),
            SyntaxFactory.BracketedArgumentList(
                SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(index.WithoutTrivia()))));
    }

    /// <summary>
    ///     Replaces the statement with parsed text and keeps the original statement's trivia.
    /// </summary>
    internal static Document ReplaceStatementFromText(
        Document document,
        SyntaxNode root,
        StatementSyntax statement,
        string text)
    {
        var replacement = SyntaxFactory.ParseStatement(text).WithTriviaFrom(statement);
        return document.WithSyntaxRoot(root.ReplaceNode(statement, replacement));
    }

    /// <summary>
    ///     Builds a field name from the receiver text. The fallback applies when no letter or digit
    ///     survives. The suffix follows the lower-cased identifier.
    /// </summary>
    internal static string BuildFieldName(string operandText, string fallback, string suffix)
    {
        var lastDot = operandText.LastIndexOf('.');
        var simpleName = lastDot >= 0 ? operandText.Substring(lastDot + 1) : operandText;

        var builder = new StringBuilder();
        foreach (var c in simpleName)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                builder.Append(c);
            }
        }

        var identifier = builder.Length > 0 ? builder.ToString() : fallback;
        return "_" + char.ToLowerInvariant(identifier[0]) + identifier.Substring(1) + suffix;
    }

    /// <summary>
    ///     True when the type declares a field, or a property or method that the caller includes,
    ///     with the name. A field variable counts as a declared member name.
    /// </summary>
    internal static bool HasMemberNamed(
        TypeDeclarationSyntax typeDecl,
        string name,
        bool includeProperties,
        bool includeMethods)
    {
        foreach (var member in typeDecl.Members)
        {
            if (DeclaresName(member, name, includeProperties, includeMethods))
            {
                return true;
            }
        }

        return false;
    }

    private static bool DeclaresName(
        MemberDeclarationSyntax member,
        string name,
        bool includeProperties,
        bool includeMethods)
    {
        return member switch
        {
            FieldDeclarationSyntax field => DeclaresVariableNamed(field, name),
            PropertyDeclarationSyntax property => includeProperties && IsNamed(property.Identifier, name),
            MethodDeclarationSyntax method => includeMethods && IsNamed(method.Identifier, name),
            _ => false
        };
    }

    private static bool DeclaresVariableNamed(FieldDeclarationSyntax field, string name)
    {
        foreach (var variable in field.Declaration.Variables)
        {
            if (IsNamed(variable.Identifier, name))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsNamed(SyntaxToken identifier, string name)
    {
        return string.Equals(identifier.ValueText, name, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Replaces the loop with the replacement statement and adds the namespace that the
    ///     replacement needs.
    /// </summary>
    internal static async Task<Document> ReplaceLoopAsync(
        Document document,
        StatementSyntax loop,
        StatementSyntax replacement,
        string namespaceName,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var newRoot = root.ReplaceNode(loop, replacement.WithTriviaFrom(loop));
        return document.WithSyntaxRoot(UsingDirectives.AddIfMissing(newRoot, namespaceName));
    }

    /// <summary>
    ///     Wraps the statement in a try block with one catch clause. The catch clause handles
    ///     <paramref name="catchTypeName" /> and runs <paramref name="catchBody" />.
    /// </summary>
    internal static async Task<Document> WrapInTryCatchAsync(
        Document document,
        StatementSyntax statement,
        string catchTypeName,
        BlockSyntax catchBody,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var tryBlock = SyntaxFactory.Block(statement.WithoutLeadingTrivia());
        var catchClause = SyntaxFactory.CatchClause(
            SyntaxFactory.CatchDeclaration(SyntaxFactory.ParseTypeName(catchTypeName)),
            null,
            catchBody);

        var tryCatch = SyntaxFactory.TryStatement(
                tryBlock,
                SyntaxFactory.SingletonList(catchClause),
                null)
            .WithLeadingTrivia(statement.GetLeadingTrivia())
            .WithTrailingTrivia(statement.GetTrailingTrivia());

        var newRoot = root.ReplaceNode(statement, tryCatch);
        return document.WithSyntaxRoot(newRoot);
    }

    /// <summary>
    ///     Adds <c lang="csharp">async</c> to the method that contains an await, and promotes its
    ///     return type to <c lang="csharp">Task</c> or <c lang="csharp">Task&lt;T&gt;</c>. A method
    ///     that is already async keeps its shape.
    /// </summary>
    internal static SyntaxNode PromoteContainingMethod(
        SyntaxNode root,
        AwaitExpressionSyntax awaitExpression)
    {
        var method = awaitExpression.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (method is null || method.Modifiers.Any(SyntaxKind.AsyncKeyword))
        {
            return root;
        }

        var asyncToken = SyntaxFactory.Token(SyntaxKind.AsyncKeyword)
            .WithTrailingTrivia(SyntaxFactory.Space);
        var newMethod = method.WithModifiers(method.Modifiers.Add(asyncToken));

        return root.ReplaceNode(method, PromoteReturnType(newMethod));
    }

    private static MethodDeclarationSyntax PromoteReturnType(MethodDeclarationSyntax method)
    {
        var returnType = method.ReturnType;
        var returnTypeText = returnType.ToString().Trim();

        if (string.Equals(returnTypeText, "void", StringComparison.Ordinal))
        {
            return method.WithReturnType(
                SyntaxFactory.IdentifierName("Task")
                    .WithTriviaFrom(returnType));
        }

        if (!returnTypeText.StartsWith("Task", StringComparison.Ordinal)
            && !returnTypeText.StartsWith("ValueTask", StringComparison.Ordinal))
        {
            var taskOfT = SyntaxFactory.GenericName(
                SyntaxFactory.Identifier("Task"),
                SyntaxFactory.TypeArgumentList(
                    SyntaxFactory.SingletonSeparatedList(returnType.WithoutTrivia())));

            return method.WithReturnType(taskOfT.WithTriviaFrom(returnType));
        }

        return method;
    }
}

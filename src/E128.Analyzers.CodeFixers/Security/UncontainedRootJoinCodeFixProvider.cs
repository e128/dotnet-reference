using System;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Security;

/// <summary>
///     Code fix for E128222: replaces <c lang="csharp">new FileInfo(Path.Combine(root.FullName, leaf))</c>
///     with <c lang="csharp">FilePathContainment.ResolveInside(root, leaf)</c>.
/// </summary>
/// <remarks>
///     The fix applies to that one shape. A join that does not feed a <c lang="csharp">FileInfo</c>
///     construction offers no fix, because the replacement type is unknown. The fix never calls
///     <c lang="csharp">Path.GetFileName</c> on the leaf, because flattening a multi-segment leaf
///     silently retargets the read or the delete to a different file.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UncontainedRootJoinCodeFixProvider))]
[Shared]
public sealed class UncontainedRootJoinCodeFixProvider : E128CodeFixProviderBase
{
    private const int FileInfoArgumentCount = 1;

    private const int JoinArgumentCount = 2;

    private const string ContainmentTypeMetadataName = "Pug.Core.FilePathContainment";

    private const string ContainmentTypeName = "FilePathContainment";

    private const string FileInfoTypeName = "System.IO.FileInfo";

    private const string ResolveInsideMethodName = "ResolveInside";

    private const string Title = "Resolve the path inside the root";

    public override ImmutableArray<string> FixableDiagnosticIds =>
        [UncontainedRootJoinAnalyzer.DiagnosticId];

    protected override async Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
        var join = node.FirstAncestorOrSelf<InvocationExpressionSyntax>();
        var construction = join is null ? null : FindFileInfoConstruction(join);

        if (join is null || construction is null)
        {
            return;
        }

        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken)
            .ConfigureAwait(false);

        if (model is null
            || !HasFilePathContainment(model.Compilation)
            || !IsSystemFileInfoConstruction(construction, model, context.CancellationToken))
        {
            return;
        }

        CodeFixRegistration.Register(
            context,
            diagnostic,
            Title,
            nameof(UncontainedRootJoinCodeFixProvider),
            ct => ApplyFixAsync(context.Document, construction, join, ct));
    }

    private static bool HasFilePathContainment(Compilation compilation)
    {
        return compilation.GetTypeByMetadataName(ContainmentTypeMetadataName) is not null;
    }

    private static bool IsSystemFileInfoConstruction(
        ObjectCreationExpressionSyntax construction,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        var type = model.GetTypeInfo(construction, cancellationToken).Type;

        return type is not null
               && string.Equals(type.ToDisplayString(), FileInfoTypeName, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Returns the <c lang="csharp">new FileInfo(...)</c> construction that takes the join as
    ///     its only argument, or null for any other shape.
    /// </summary>
    private static ObjectCreationExpressionSyntax? FindFileInfoConstruction(InvocationExpressionSyntax join)
    {
        SyntaxNode value = join;

        while (value.Parent is ParenthesizedExpressionSyntax parenthesized)
        {
            value = parenthesized;
        }

        if (value.Parent is not ArgumentSyntax argument
            || argument.Parent is not ArgumentListSyntax argumentList
            || argumentList.Parent is not ObjectCreationExpressionSyntax construction
            || argumentList.Arguments.Count != FileInfoArgumentCount
            || !IsFileInfoTypeName(construction.Type)
            || !ReferenceEquals(
                UnwrapParentheses(argumentList.Arguments[0].Expression),
                join))
        {
            return null;
        }

        return construction;
    }

    private static bool IsFileInfoTypeName(TypeSyntax type)
    {
        var name = type switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
            AliasQualifiedNameSyntax aliasQualified => aliasQualified.Name.Identifier.ValueText,
            _ => null
        };

        return name is not null && string.Equals(name, "FileInfo", StringComparison.Ordinal);
    }

    private static async Task<Document> ApplyFixAsync(
        Document document,
        ObjectCreationExpressionSyntax construction,
        InvocationExpressionSyntax join,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

        if (root is null || join.ArgumentList.Arguments.Count != JoinArgumentCount)
        {
            return document;
        }

        var firstArgument = UnwrapParentheses(join.ArgumentList.Arguments[0].Expression);

        if (firstArgument is not MemberAccessExpressionSyntax fullNameAccess)
        {
            return document;
        }

        var replacement = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName(ContainmentTypeName),
                    SyntaxFactory.IdentifierName(ResolveInsideMethodName)),
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SeparatedList(
                        [
                            SyntaxFactory.Argument(fullNameAccess.Expression.WithoutTrivia()),
                            SyntaxFactory.Argument(join.ArgumentList.Arguments[1].Expression.WithoutTrivia())
                        ])))
            .WithTriviaFrom(construction);

        var newRoot = root.ReplaceNode(construction, replacement);
        newRoot = UsingDirectives.AddIfMissing(newRoot, "Pug.Core");

        return document.WithSyntaxRoot(newRoot);
    }

    private static ExpressionSyntax UnwrapParentheses(ExpressionSyntax expression)
    {
        var value = expression;

        while (value is ParenthesizedExpressionSyntax parenthesized)
        {
            value = parenthesized.Expression;
        }

        return value;
    }
}

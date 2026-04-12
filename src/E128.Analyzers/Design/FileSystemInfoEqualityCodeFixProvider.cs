using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Design;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(FileSystemInfoEqualityCodeFixProvider))]
[Shared]
public sealed class FileSystemInfoEqualityCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [FileSystemInfoEqualityAnalyzer.DiagnosticId];

    public override FixAllProvider? GetFixAllProvider() =>
        WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        if (FindBinaryExpression(node) is { } binary)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Compare .FullName instead",
                    createChangedDocument: ct => FixBinaryExpressionAsync(context.Document, root, binary, ct),
                    equivalenceKey: nameof(FileSystemInfoEqualityCodeFixProvider)),
                diagnostic);
        }
    }

    private static BinaryExpressionSyntax? FindBinaryExpression(SyntaxNode node)
    {
        var current = node;
        while (current is not null)
        {
            if (current is BinaryExpressionSyntax binary
                && (binary.IsKind(SyntaxKind.EqualsExpression) || binary.IsKind(SyntaxKind.NotEqualsExpression)))
            {
                return binary;
            }

            current = current.Parent;
        }

        return null;
    }

    private static Task<Document> FixBinaryExpressionAsync(
        Document document, SyntaxNode root, BinaryExpressionSyntax binary, CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        var leftFullName = CreateFullNameAccess(binary.Left);
        var rightFullName = CreateFullNameAccess(binary.Right);

        var stringEquals = CreateStringEqualsInvocation(leftFullName, rightFullName);

        ExpressionSyntax replacement = binary.IsKind(SyntaxKind.NotEqualsExpression)
            ? SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, stringEquals)
            : stringEquals;

        replacement = replacement.WithTriviaFrom(binary);

        var newRoot = root.ReplaceNode(binary, replacement);
        newRoot = EnsureSystemUsing(newRoot);
        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }

    private static MemberAccessExpressionSyntax CreateFullNameAccess(ExpressionSyntax expression)
    {
        return SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            expression.WithoutTrivia(),
            SyntaxFactory.IdentifierName("FullName"));
    }

    private static InvocationExpressionSyntax CreateStringEqualsInvocation(
        ExpressionSyntax left, ExpressionSyntax right)
    {
        var stringEqualsAccess = SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword)),
            SyntaxFactory.IdentifierName("Equals"));

        var ordinalArg = SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            SyntaxFactory.IdentifierName("StringComparison"),
            SyntaxFactory.IdentifierName("Ordinal"));

        return SyntaxFactory.InvocationExpression(
            stringEqualsAccess,
            SyntaxFactory.ArgumentList(
                SyntaxFactory.SeparatedList(
                [
                    SyntaxFactory.Argument(left),
                    SyntaxFactory.Argument(right),
                    SyntaxFactory.Argument(ordinalArg),
                ])));
    }

    private static SyntaxNode EnsureSystemUsing(SyntaxNode root)
    {
        if (root is not CompilationUnitSyntax compilationUnit)
        {
            return root;
        }

        var hasSystem = false;
        foreach (var directive in compilationUnit.Usings)
        {
            if (string.Equals(directive.Name?.ToString(), "System", System.StringComparison.Ordinal))
            {
                hasSystem = true;
                break;
            }
        }

        if (hasSystem)
        {
            return root;
        }

        var usingDirective = SyntaxFactory.UsingDirective(
            SyntaxFactory.ParseName("System"))
            .WithTrailingTrivia(SyntaxFactory.LineFeed);

        return compilationUnit.WithUsings(compilationUnit.Usings.Insert(0, usingDirective));
    }
}

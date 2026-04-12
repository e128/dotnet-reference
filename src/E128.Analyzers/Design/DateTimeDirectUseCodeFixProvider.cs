using System;
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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DateTimeDirectUseCodeFixProvider))]
[Shared]
public sealed class DateTimeDirectUseCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [DateTimeDirectUseAnalyzer.DiagnosticId];

    public override FixAllProvider GetFixAllProvider() =>
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

        if (node is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        var replacement = GetReplacement(memberAccess);
        if (replacement is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Replace with TimeProvider.System",
                createChangedDocument: ct => ApplyFixAsync(context.Document, memberAccess, replacement, ct),
                equivalenceKey: nameof(DateTimeDirectUseCodeFixProvider)),
            diagnostic);
    }

    private static ExpressionSyntax? GetReplacement(MemberAccessExpressionSyntax memberAccess)
    {
        var typeName = memberAccess.Expression.ToString();
        var memberName = memberAccess.Name.Identifier.ValueText;

        // TimeProvider.System base expression
        var timeProviderSystem = SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            SyntaxFactory.IdentifierName("TimeProvider"),
            SyntaxFactory.IdentifierName("System"));

        return string.Equals(typeName, "DateTime", StringComparison.Ordinal)
            ? memberName switch
            {
                "UtcNow" => BuildChain(timeProviderSystem, "GetUtcNow", "UtcDateTime"),
                "Now" => BuildChain(timeProviderSystem, "GetLocalNow", "DateTime"),
                "Today" => BuildChain(timeProviderSystem, "GetLocalNow", "Date"),
                _ => null,
            }
            : string.Equals(typeName, "DateTimeOffset", StringComparison.Ordinal)
            ? memberName switch
            {
                "UtcNow" => BuildMethodCall(timeProviderSystem, "GetUtcNow"),
                "Now" => BuildMethodCall(timeProviderSystem, "GetLocalNow"),
                _ => null,
            }
            : (ExpressionSyntax?)null;
    }

    private static MemberAccessExpressionSyntax BuildChain(
        ExpressionSyntax receiver,
        string methodName,
        string propertyName)
    {
        var call = BuildMethodCall(receiver, methodName);

        return SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            call,
            SyntaxFactory.IdentifierName(propertyName));
    }

    private static InvocationExpressionSyntax BuildMethodCall(
        ExpressionSyntax receiver,
        string methodName)
    {
        return SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                receiver,
                SyntaxFactory.IdentifierName(methodName)));
    }

    private static async Task<Document> ApplyFixAsync(
        Document document,
        MemberAccessExpressionSyntax memberAccess,
        ExpressionSyntax replacement,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var newRoot = root.ReplaceNode(memberAccess, replacement.WithTriviaFrom(memberAccess));
        return document.WithSyntaxRoot(newRoot);
    }
}

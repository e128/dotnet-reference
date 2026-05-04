using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace E128.Analyzers.Performance;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(LinearLookupInLoopCodeFixProvider))]
[Shared]
public sealed class LinearLookupInLoopCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [LinearLookupInLoopAnalyzer.DiagnosticId];

    public override FixAllProvider GetFixAllProvider()
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
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        if (node is not InvocationExpressionSyntax invocation)
        {
            return;
        }

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        var methodName = memberAccess.Name.Identifier.ValueText;

        if (!string.Equals(methodName, "Contains", StringComparison.Ordinal)
            && !string.Equals(methodName, "Any", StringComparison.Ordinal))
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Convert to HashSet<T> for O(1) lookup",
                ct => ApplyToHashSetFixAsync(context.Document, invocation, memberAccess, ct),
                nameof(LinearLookupInLoopCodeFixProvider)),
            diagnostic);
    }

    private static async Task<Document> ApplyToHashSetFixAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax memberAccess,
        CancellationToken cancellationToken)
    {
        var receiverName = memberAccess.Expression is IdentifierNameSyntax id
            ? id.Identifier.ValueText
            : null;

        if (receiverName is null)
        {
            return document;
        }

        var loopOrLinqNode = FindEnclosingLoop(invocation);
        if (loopOrLinqNode is null)
        {
            return document;
        }

        var loopStatement = loopOrLinqNode is InvocationExpressionSyntax
            ? FindContainingStatement(loopOrLinqNode)
            : loopOrLinqNode;
        if (loopStatement is null)
        {
            return document;
        }

        var setVarName = receiverName + "Set";
        var setDeclaration = BuildToHashSetDeclaration(receiverName, setVarName, loopStatement);

        var newReceiver = SyntaxFactory.IdentifierName(setVarName);
        var newMemberAccess = memberAccess.WithExpression(newReceiver);
        var newInvocation = invocation.WithExpression(newMemberAccess);

        var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        editor.InsertBefore(loopStatement, setDeclaration);
        editor.ReplaceNode(invocation, newInvocation);

        var newDocument = editor.GetChangedDocument();
        return await EnsureUsingDirectiveAsync(newDocument, "System.Linq", cancellationToken).ConfigureAwait(false);
    }

    private static LocalDeclarationStatementSyntax BuildToHashSetDeclaration(
        string receiverName,
        string setVarName,
        SyntaxNode loopStatement)
    {
        var toHashSetCall = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName(receiverName),
                SyntaxFactory.IdentifierName("ToHashSet")));

        return SyntaxFactory.LocalDeclarationStatement(
                SyntaxFactory.VariableDeclaration(
                        SyntaxFactory.IdentifierName("var"))
                    .WithVariables(
                        SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory.VariableDeclarator(
                                    SyntaxFactory.Identifier(setVarName))
                                .WithInitializer(
                                    SyntaxFactory.EqualsValueClause(toHashSetCall)))))
            .WithLeadingTrivia(loopStatement.GetLeadingTrivia())
            .WithTrailingTrivia(SyntaxFactory.LineFeed);
    }

    private static SyntaxNode? FindEnclosingLoop(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (current is ForEachStatementSyntax or ForStatementSyntax or WhileStatementSyntax or DoStatementSyntax)
            {
                return current;
            }

            if (current is LambdaExpressionSyntax
                && current.Parent is ArgumentSyntax { Parent: ArgumentListSyntax { Parent: InvocationExpressionSyntax linqInvocation } })
            {
                return linqInvocation;
            }

            if (current is MethodDeclarationSyntax or ConstructorDeclarationSyntax)
            {
                return null;
            }
        }

        return null;
    }

    private static StatementSyntax? FindContainingStatement(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (current is StatementSyntax statement)
            {
                return statement;
            }
        }

        return null;
    }

    private static async Task<Document> EnsureUsingDirectiveAsync(
        Document document,
        string namespaceName,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is not CompilationUnitSyntax compilationUnit)
        {
            return document;
        }

        if (compilationUnit.Usings.Any(u =>
                string.Equals(u.Name?.ToString(), namespaceName, StringComparison.Ordinal)))
        {
            return document;
        }

        var usingDirective = SyntaxFactory.UsingDirective(
                SyntaxFactory.ParseName(namespaceName))
            .WithTrailingTrivia(SyntaxFactory.LineFeed);

        var lastUsing = compilationUnit.Usings.LastOrDefault();
        var newRoot = lastUsing is not null
            ? root.InsertNodesAfter(lastUsing, new[] { usingDirective })
            : compilationUnit.WithUsings(SyntaxFactory.SingletonList(usingDirective));

        return document.WithSyntaxRoot(newRoot);
    }
}

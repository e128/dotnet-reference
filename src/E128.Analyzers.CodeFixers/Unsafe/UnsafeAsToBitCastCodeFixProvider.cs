using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Unsafe;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UnsafeAsToBitCastCodeFixProvider))]
[Shared]
public sealed class UnsafeAsToBitCastCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [UnsafeAsToBitCastAnalyzer.DiagnosticId];

    public override FixAllProvider? GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        if (node is not InvocationExpressionSyntax
            {
                Expression: MemberAccessExpressionSyntax { Name: GenericNameSyntax generic } memberAccess,
            } invocation
            || invocation.ArgumentList.Arguments.Count != 1)
        {
            return Task.CompletedTask;
        }

        var argument = invocation.ArgumentList.Arguments[0];
        var typeArguments = generic.TypeArgumentList;

        context.RegisterCodeFix(
            CodeAction.Create(
                "Use Unsafe.BitCast",
                _ => Task.FromResult(ApplyFix(context.Document, root, invocation, memberAccess, argument, typeArguments)),
                nameof(UnsafeAsToBitCastCodeFixProvider)),
            diagnostic);

        return Task.CompletedTask;
    }

    private static Document ApplyFix(
        Document document,
        SyntaxNode root,
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax memberAccess,
        ArgumentSyntax argument,
        TypeArgumentListSyntax typeArguments)
    {
        var receiver = memberAccess.Expression.ToString();
        var arguments = typeArguments.Arguments.ToString();
        var value = argument.Expression.ToString();
        var text = receiver + ".BitCast<" + arguments + ">(" + value + ")";

        var replacement = SyntaxFactory.ParseExpression(text).WithTriviaFrom(invocation);
        return document.WithSyntaxRoot(root.ReplaceNode(invocation, replacement));
    }
}

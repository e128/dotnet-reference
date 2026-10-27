using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Unsafe;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UnsafeReadWriteAlignmentCodeFixProvider))]
[Shared]
public sealed class UnsafeReadWriteAlignmentCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [UnsafeReadWriteAlignmentAnalyzer.DiagnosticId];

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
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        if (node is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax memberAccess })
        {
            return;
        }

        if (!UnsafeReadWriteAlignmentAnalyzer.IsUnalignedVariant(
                memberAccess.Name.Identifier.ValueText,
                out var replacement))
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Use Unsafe." + replacement,
                _ => Task.FromResult(ApplyFix(context.Document, root, memberAccess, replacement)),
                nameof(UnsafeReadWriteAlignmentCodeFixProvider)),
            diagnostic);
    }

    private static Document ApplyFix(
        Document document,
        SyntaxNode root,
        MemberAccessExpressionSyntax memberAccess,
        string replacement)
    {
        SimpleNameSyntax newName = memberAccess.Name is GenericNameSyntax generic
            ? SyntaxFactory
                .GenericName(SyntaxFactory.Identifier(replacement), generic.TypeArgumentList)
                .WithTriviaFrom(memberAccess.Name)
            : SyntaxFactory.IdentifierName(replacement).WithTriviaFrom(memberAccess.Name);

        return document.WithSyntaxRoot(root.ReplaceNode(memberAccess.Name, newName));
    }
}

using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Unsafe;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UnsafeReadWriteAlignmentCodeFixProvider))]
[Shared]
public sealed class UnsafeReadWriteAlignmentCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [UnsafeReadWriteAlignmentAnalyzer.DiagnosticId];

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        if (node is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax memberAccess })
        {
            return Task.CompletedTask;
        }

        if (!UnsafeReadWriteAlignmentAnalyzer.IsUnalignedVariant(
                memberAccess.Name.Identifier.ValueText,
                out var replacement))
        {
            return Task.CompletedTask;
        }

        CodeFixRegistration.Register(context, diagnostic, "Use Unsafe." + replacement, nameof(UnsafeReadWriteAlignmentCodeFixProvider), _ => Task.FromResult(ApplyFix(context.Document, root, memberAccess, replacement)));

        return Task.CompletedTask;
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

using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Unsafe;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UnsafeAsToBitCastCodeFixProvider))]
[Shared]
public sealed class UnsafeAsToBitCastCodeFixProvider : E128CodeFixProviderBase
{
    private const int ExpectedTypeArgumentCount = 2;

    public override ImmutableArray<string> FixableDiagnosticIds =>
        [UnsafeAsToBitCastAnalyzer.DiagnosticId];

    protected override async Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        if (node is not InvocationExpressionSyntax
            {
                Expression: MemberAccessExpressionSyntax { Name: GenericNameSyntax generic } memberAccess,
            } invocation
            || invocation.ArgumentList.Arguments.Count is not 1)
        {
            return;
        }

        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (model is null
            || model.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || method.TypeArguments.Length is not ExpectedTypeArgumentCount
            || method.TypeArguments.Any(static type => type.IsReferenceType || !type.IsUnmanagedType))
        {
            return;
        }

        var argument = invocation.ArgumentList.Arguments[0];
        var typeArguments = generic.TypeArgumentList;

        CodeFixRegistration.Register(context, diagnostic, "Use Unsafe.BitCast", nameof(UnsafeAsToBitCastCodeFixProvider), _ => Task.FromResult(ApplyFix(context.Document, root, invocation, memberAccess, argument, typeArguments)));
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

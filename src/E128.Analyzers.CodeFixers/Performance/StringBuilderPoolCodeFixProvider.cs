using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Performance;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(StringBuilderPoolCodeFixProvider))]
[Shared]
public sealed class StringBuilderPoolCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [StringBuilderPoolAnalyzer.DiagnosticId];

    protected override async Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return;
        }

        var poolType = semanticModel.Compilation.GetTypeByMetadataName(
            StringBuilderPoolAnalyzer.StringBuilderPoolMetadataName);
        if (poolType is null)
        {
            return;
        }

        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        if (node is not (ObjectCreationExpressionSyntax or ImplicitObjectCreationExpressionSyntax)
            || !CanReplaceWithRent(node, semanticModel, context.CancellationToken))
        {
            return;
        }

        CodeFixRegistration.Register(context, diagnostic, "Replace with StringBuilderPool.Shared.Rent()", nameof(StringBuilderPoolCodeFixProvider), ct => ApplyFixAsync(context.Document, node, ct));
    }

    // Rent() takes no seed. A seed string carries content the rewrite would drop. A capacity hint
    // only costs a later resize, so it is safe to drop.
    private static bool CanReplaceWithRent(
        SyntaxNode creation,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var arguments = creation switch
        {
            ObjectCreationExpressionSyntax explicitCreation => explicitCreation.ArgumentList?.Arguments ?? default,
            ImplicitObjectCreationExpressionSyntax implicitCreation => implicitCreation.ArgumentList.Arguments,
            _ => default
        };

        return arguments.Count is 0
               || (arguments.Count is 1
                   && semanticModel.GetTypeInfo(arguments[0].Expression, cancellationToken).Type?.SpecialType
                       is SpecialType.System_Int32);
    }

    private static async Task<Document> ApplyFixAsync(
        Document document,
        SyntaxNode creationNode,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var sharedAccess = SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            SyntaxFactory.IdentifierName("StringBuilderPool"),
            SyntaxFactory.IdentifierName("Shared"));

        var rentAccess = SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            sharedAccess,
            SyntaxFactory.IdentifierName("Rent"));

        var rentInvocation = SyntaxFactory.InvocationExpression(
                rentAccess,
                SyntaxFactory.ArgumentList())
            .WithTriviaFrom(creationNode);

        var newRoot = UsingDirectives.AddIfMissing(
            root.ReplaceNode(creationNode, rentInvocation),
            StringBuilderPoolAnalyzer.StringBuilderPoolNamespace);

        return document.WithSyntaxRoot(newRoot);
    }
}

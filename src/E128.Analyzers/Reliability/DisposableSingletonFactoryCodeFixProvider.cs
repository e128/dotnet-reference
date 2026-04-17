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

namespace E128.Analyzers.Reliability;

/// <summary>
///     Code fix for E128031: rewrites <c>AddSingleton(sp => new T())</c> to
///     <c>AddSingleton&lt;IFoo, T&gt;()</c> when the concrete type implements a non-marker interface.
///     Offers one code action per non-marker interface the type implements.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DisposableSingletonFactoryCodeFixProvider))]
[Shared]
public sealed class DisposableSingletonFactoryCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [DisposableSingletonFactoryAnalyzer.DiagnosticId];

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

        var invocation = node.FirstAncestorOrSelf<InvocationExpressionSyntax>();
        if (invocation is null)
        {
            return;
        }

        var returnType = await ResolveReturnTypeAsync(context, invocation).ConfigureAwait(false);
        if (returnType is null)
        {
            return;
        }

        var nonMarkerInterfaces = returnType.AllInterfaces
            .Where(i => !IsMarkerInterface(i))
            .ToList();

        foreach (var iface in nonMarkerInterfaces)
        {
            var title = $"Register as AddSingleton<{iface.Name}, {returnType.Name}>()";
            context.RegisterCodeFix(
                CodeAction.Create(
                    title,
                    ct => RewriteToGenericOverloadAsync(context.Document, invocation, returnType, iface, ct),
                    $"E128031_{iface.ToDisplayString()}"),
                diagnostic);
        }
    }

    private static bool IsMarkerInterface(INamedTypeSymbol iface)
    {
        var display = iface.ToDisplayString();
        return string.Equals(display, "System.IDisposable", StringComparison.Ordinal)
               || string.Equals(display, "System.IAsyncDisposable", StringComparison.Ordinal);
    }

    private static async Task<ITypeSymbol?> ResolveReturnTypeAsync(
        CodeFixContext context,
        InvocationExpressionSyntax invocation)
    {
        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return null;
        }

        var arg = invocation.ArgumentList.Arguments[0].Expression;
        var bodyExpr = arg switch
        {
            SimpleLambdaExpressionSyntax s => s.Body as ExpressionSyntax,
            ParenthesizedLambdaExpressionSyntax p => p.Body as ExpressionSyntax,
            _ => null
        };

        return bodyExpr is null ? null : semanticModel.GetTypeInfo(bodyExpr, context.CancellationToken).Type;
    }

    private static async Task<Document> RewriteToGenericOverloadAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        ITypeSymbol concreteType,
        INamedTypeSymbol targetInterface,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return document;
        }

        var newTypeArgs = SyntaxFactory.TypeArgumentList(
            SyntaxFactory.SeparatedList(
                [
                    SyntaxFactory.ParseTypeName(targetInterface.Name),
                    SyntaxFactory.ParseTypeName(concreteType.Name)
                ]));

        var newName = SyntaxFactory.GenericName(
                SyntaxFactory.Identifier("AddSingleton"))
            .WithTypeArgumentList(newTypeArgs);

        var newMemberAccess = memberAccess.WithName(newName);
        var newInvocation = invocation
            .WithExpression(newMemberAccess)
            .WithArgumentList(SyntaxFactory.ArgumentList());

        var newRoot = root.ReplaceNode(invocation, newInvocation);
        return document.WithSyntaxRoot(newRoot);
    }
}

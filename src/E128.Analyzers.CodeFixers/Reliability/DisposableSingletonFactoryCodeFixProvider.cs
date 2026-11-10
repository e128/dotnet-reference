using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Reliability;

/// <summary>
///     Code fix for E128031: rewrites <c lang="csharp">AddSingleton(sp => new T())</c> to
///     <c lang="csharp">AddSingleton&lt;IFoo, T&gt;()</c> when the concrete type implements a non-marker interface.
///     Offers one code action per non-marker interface the type implements.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DisposableSingletonFactoryCodeFixProvider))]
[Shared]
public sealed class DisposableSingletonFactoryCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [DisposableSingletonFactoryAnalyzer.DiagnosticId];

    protected override async Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        var invocation = node.FirstAncestorOrSelf<InvocationExpressionSyntax>();
        if (invocation is null)
        {
            return;
        }

        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return;
        }

        var returnType = ResolveReturnType(semanticModel, invocation, context.CancellationToken);
        if (returnType is null)
        {
            return;
        }

        // The two-type-argument overload asks the container to construct the type itself. A factory
        // that passes any argument builds something the container cannot reproduce.
        if (!FixSyntax.IsParameterlessCreation(invocation.ArgumentList.Arguments[0].Expression))
        {
            return;
        }

        var nonMarkerInterfaces = returnType.AllInterfaces
            .Where(static i => !ConcreteOnlyDiRegistrationAnalyzer.MarkerInterfaces.Contains(i.ToDisplayString()))
            .ToList();

        foreach (var iface in nonMarkerInterfaces)
        {
            // The minimal form keeps the type arguments and matches the text the fix writes.
            var title = $"Register as AddSingleton<{iface.ToMinimalDisplayString(semanticModel, invocation.SpanStart)}, {returnType.ToMinimalDisplayString(semanticModel, invocation.SpanStart)}>()";
            CodeFixRegistration.Register(context, diagnostic, title, $"E128031_{iface.ToDisplayString()}", ct => DiRegistrationFix.RewriteToGenericAsync(context.Document, invocation, returnType, iface, SyntaxFactory.Identifier("AddSingleton"), ct));
        }
    }

    private static ITypeSymbol? ResolveReturnType(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        CancellationToken cancellationToken)
    {
        var arg = invocation.ArgumentList.Arguments[0].Expression;
        var bodyExpr = arg switch
        {
            SimpleLambdaExpressionSyntax s => s.Body as ExpressionSyntax,
            ParenthesizedLambdaExpressionSyntax p => p.Body as ExpressionSyntax,
            _ => null
        };

        return bodyExpr is null ? null : semanticModel.GetTypeInfo(bodyExpr, cancellationToken).Type;
    }
}

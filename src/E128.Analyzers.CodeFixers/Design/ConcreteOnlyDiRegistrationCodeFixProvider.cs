using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Design;

/// <summary>
///     Code fix for E128032: rewrites <c lang="csharp">Add{Lifetime}&lt;TImpl&gt;()</c> to
///     <c lang="csharp">Add{Lifetime}&lt;IFoo, TImpl&gt;()</c>. When the concrete type implements
///     multiple non-marker interfaces, one <see cref="CodeAction" /> is offered per interface.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ConcreteOnlyDiRegistrationCodeFixProvider))]
[Shared]
public sealed class ConcreteOnlyDiRegistrationCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [ConcreteOnlyDiRegistrationAnalyzer.DiagnosticId];

    protected override async Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        var invocation = node.FirstAncestorOrSelf<InvocationExpressionSyntax>();
        if (invocation is null)
        {
            return;
        }

        // An unqualified registration through `using static` has no receiver to rewrite, so
        // offering a fix would register an action that changes nothing.
        if (invocation.Expression is not MemberAccessExpressionSyntax)
        {
            return;
        }

        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return;
        }

        if (semanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol methodSymbol
            || methodSymbol.TypeArguments.Length != 1)
        {
            return;
        }

        // The two-type-argument overload takes no factory. A factory that passes an argument builds
        // something the container cannot reproduce, so the registration must stay as it is.
        if (!HasNoFactoryOrParameterlessFactory(invocation))
        {
            return;
        }

        RegisterInterfaceFixes(context, invocation, semanticModel, methodSymbol, diagnostic);
    }

    private static void RegisterInterfaceFixes(
        CodeFixContext context,
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        IMethodSymbol methodSymbol,
        Diagnostic diagnostic)
    {
        var concreteType = methodSymbol.TypeArguments[0];
        var nonMarkerInterfaces = concreteType.AllInterfaces
            .Where(i => !ConcreteOnlyDiRegistrationAnalyzer.MarkerInterfaces.Contains(i.ToDisplayString()))
            .ToList();

        foreach (var iface in nonMarkerInterfaces)
        {
            var title = $"Register as Add{methodSymbol.Name.Substring(3)}"
                        + $"<{iface.ToMinimalDisplayString(semanticModel, invocation.SpanStart)}, "
                        + $"{concreteType.ToMinimalDisplayString(semanticModel, invocation.SpanStart)}>()";
            CodeFixRegistration.Register(context, diagnostic, title, $"E128032_{iface.ToDisplayString()}", ct => DiRegistrationFix.RewriteToGenericAsync(context.Document, invocation, concreteType, iface, null, ct));
        }
    }

    private static bool HasNoFactoryOrParameterlessFactory(InvocationExpressionSyntax invocation)
    {
        if (invocation.ArgumentList.Arguments.Count == 0)
        {
            return true;
        }

        if (invocation.ArgumentList.Arguments.Count > 1)
        {
            return false;
        }

        var body = invocation.ArgumentList.Arguments[0].Expression switch
        {
            SimpleLambdaExpressionSyntax simple => simple.Body as ExpressionSyntax,
            ParenthesizedLambdaExpressionSyntax parenthesized => parenthesized.Body as ExpressionSyntax,
            _ => null
        };

        return body is ObjectCreationExpressionSyntax creation
               && (creation.ArgumentList?.Arguments.Count ?? 0) == 0;
    }
}

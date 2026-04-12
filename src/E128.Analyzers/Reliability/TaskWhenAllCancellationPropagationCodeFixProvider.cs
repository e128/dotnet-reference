using System;
using System.Collections.Generic;
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
/// Code fix for E128038: appends the enclosing method's <c>CancellationToken</c> parameter
/// to HttpClient/Playwright method calls inside the async lambda that are missing it.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TaskWhenAllCancellationPropagationCodeFixProvider))]
[Shared]
public sealed class TaskWhenAllCancellationPropagationCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [TaskWhenAllCancellationPropagationAnalyzer.DiagnosticId];

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

        if (node is not InvocationExpressionSyntax invocation)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Add CancellationToken to HttpClient/Playwright calls",
                createChangedDocument: ct => AddCancellationTokenAsync(context.Document, invocation, ct),
                equivalenceKey: nameof(TaskWhenAllCancellationPropagationCodeFixProvider)),
            diagnostic);
    }

    private static async Task<Document> AddCancellationTokenAsync(
        Document document,
        InvocationExpressionSyntax whenAllInvocation,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return document;
        }

        var ctParamName = FindEnclosingCtParameterName(whenAllInvocation, semanticModel, cancellationToken);
        if (ctParamName is null)
        {
            return document;
        }

        var callsToFix = FindCallsMissingCt(whenAllInvocation, semanticModel, cancellationToken);
        if (callsToFix.Count == 0)
        {
            return document;
        }

        var ctArgument = SyntaxFactory.Argument(SyntaxFactory.IdentifierName(ctParamName));

        var newRoot = root.ReplaceNodes(callsToFix, (original, rewritten) =>
        {
            var newArgList = rewritten.ArgumentList.AddArguments(ctArgument);
            return rewritten.WithArgumentList(newArgList);
        });

        return document.WithSyntaxRoot(newRoot);
    }

    private static string? FindEnclosingCtParameterName(
        InvocationExpressionSyntax whenAllInvocation,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var enclosingMethod = whenAllInvocation.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        return enclosingMethod is null ? null : FindCancellationTokenParameterName(enclosingMethod, semanticModel, cancellationToken);
    }

    private static List<InvocationExpressionSyntax> FindCallsMissingCt(
        InvocationExpressionSyntax whenAllInvocation,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var whenAllArgs = whenAllInvocation.ArgumentList.Arguments;
        if (whenAllArgs.Count != 1
            || whenAllArgs[0].Expression is not InvocationExpressionSyntax selectInvocation)
        {
            return [];
        }

        var selectArgs = selectInvocation.ArgumentList.Arguments;
        if (!selectArgs.Any())
        {
            return [];
        }

        var lambdaArg = selectArgs.Last().Expression;

        return [.. lambdaArg.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => IsHttpClientOrPlaywrightMethod(inv)
                && !HasCancellationTokenArgument(semanticModel, inv, cancellationToken))];
    }

    private static string? FindCancellationTokenParameterName(
        MethodDeclarationSyntax method,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        foreach (var param in method.ParameterList.Parameters)
        {
            if (param.Type is null)
            {
                continue;
            }

            var typeInfo = semanticModel.GetTypeInfo(param.Type, cancellationToken);
            if (string.Equals(typeInfo.Type?.ToDisplayString(), "System.Threading.CancellationToken", StringComparison.Ordinal))
            {
                return param.Identifier.ValueText;
            }
        }

        return null;
    }

    private static bool IsHttpClientOrPlaywrightMethod(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax { Name: IdentifierNameSyntax name })
        {
            return false;
        }

        var methodName = name.Identifier.Text;
        return methodName switch
        {
            "GetAsync" or "PostAsync" or "SendAsync" or "PutAsync" or "DeleteAsync"
                or "PatchAsync" or "GetStringAsync" or "GetStreamAsync" or "GetByteArrayAsync"
                or "GotoAsync" or "ClickAsync" or "FillAsync" or "TypeAsync" or "TapAsync"
                or "CheckAsync" or "UncheckAsync" or "SelectOptionAsync" or "HoverAsync"
                or "FocusAsync" or "PressAsync" or "DispatchEventAsync" or "WaitForSelectorAsync"
                or "WaitForNavigationAsync" or "WaitForURLAsync" or "WaitForLoadStateAsync" => true,
            _ => false,
        };
    }

    private static bool HasCancellationTokenArgument(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        CancellationToken cancellationToken)
    {
        foreach (var arg in invocation.ArgumentList.Arguments)
        {
            var typeInfo = semanticModel.GetTypeInfo(arg.Expression, cancellationToken);
            if (string.Equals(typeInfo.Type?.ToDisplayString(), "System.Threading.CancellationToken", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}

using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
/// E128038: Flags <c>Task.WhenAll(collection.Select(async x => await httpClient.Method(url)))</c>
/// where the enclosing async method has a <see cref="System.Threading.CancellationToken"/> parameter
/// but that token is not forwarded to the HttpClient or Playwright method inside the lambda.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TaskWhenAllCancellationPropagationAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128038";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Task.WhenAll async lambda missing CancellationToken propagation",
        messageFormat: "The async lambda inside Task.WhenAll calls an HttpClient or Playwright method without passing the enclosing CancellationToken",
        category: "Reliability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "When an enclosing async method has a CancellationToken parameter, " +
            "async lambdas inside Task.WhenAll must propagate that token to HttpClient and Playwright methods. " +
            "Without propagation, cancellation of the outer operation does not cancel the individual tasks.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (!IsTaskWhenAll(context, invocation))
        {
            return;
        }

        var args = invocation.ArgumentList.Arguments;
        if (args.Count != 1)
        {
            return;
        }

        if (!TryGetSelectInvocation(args[0].Expression, out var selectInvocation))
        {
            return;
        }

        if (!HasAsyncLambda(selectInvocation))
        {
            return;
        }

        var enclosingMethod = invocation.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (enclosingMethod is null || !EnclosingMethodHasCancellationToken(context, enclosingMethod))
        {
            return;
        }

        var lambdaArg = selectInvocation.ArgumentList.Arguments.Last().Expression;
        var hasUnpropagatedCall = lambdaArg.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Any(inv => IsHttpClientOrPlaywrightMethod(inv)
                && !HasCancellationTokenArgument(context, inv));

        if (hasUnpropagatedCall)
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
        }
    }

    private static bool IsTaskWhenAll(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation)
    {
        var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken);
        return symbolInfo.Symbol is IMethodSymbol method
            && string.Equals(method.Name, "WhenAll", StringComparison.Ordinal)
            && string.Equals(method.ContainingType?.ToDisplayString(), "System.Threading.Tasks.Task", StringComparison.Ordinal);
    }

    private static bool TryGetSelectInvocation(ExpressionSyntax expression, out InvocationExpressionSyntax selectInvocation)
    {
        selectInvocation = null!;

        if (expression is not InvocationExpressionSyntax candidate)
        {
            return false;
        }

        if (candidate.Expression is MemberAccessExpressionSyntax memberAccess
            && string.Equals(memberAccess.Name.Identifier.ValueText, "Select", StringComparison.Ordinal))
        {
            selectInvocation = candidate;
            return true;
        }

        return false;
    }

    private static bool HasAsyncLambda(InvocationExpressionSyntax selectInvocation)
    {
        var args = selectInvocation.ArgumentList.Arguments;
        if (!args.Any())
        {
            return false;
        }

        var lambdaArg = args.Last().Expression;
        return lambdaArg switch
        {
            ParenthesizedLambdaExpressionSyntax pLambda => pLambda.AsyncKeyword.IsKind(SyntaxKind.AsyncKeyword),
            SimpleLambdaExpressionSyntax sLambda => sLambda.AsyncKeyword.IsKind(SyntaxKind.AsyncKeyword),
            _ => false,
        };
    }

    private static bool EnclosingMethodHasCancellationToken(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax method)
    {
        return method.ParameterList.Parameters.Any(p =>
        {
            if (p.Type is null)
            {
                return false;
            }

            var typeInfo = context.SemanticModel.GetTypeInfo(p.Type, context.CancellationToken);
            return string.Equals(
                typeInfo.Type?.ToDisplayString(),
                "System.Threading.CancellationToken",
                StringComparison.Ordinal);
        });
    }

    private static bool IsHttpClientOrPlaywrightMethod(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax { Name: IdentifierNameSyntax name })
        {
            return false;
        }

        var methodName = name.Identifier.Text;
        return IsHttpClientMethod(methodName) || IsPlaywrightMethod(methodName);
    }

    private static bool IsHttpClientMethod(string name) => name switch
    {
        "GetAsync" or "PostAsync" or "SendAsync" or "PutAsync" or "DeleteAsync"
            or "PatchAsync" or "GetStringAsync" or "GetStreamAsync" or "GetByteArrayAsync" => true,
        _ => false,
    };

    private static bool IsPlaywrightMethod(string name) => name switch
    {
        "GotoAsync" or "ClickAsync" or "FillAsync" or "TypeAsync" or "TapAsync"
            or "CheckAsync" or "UncheckAsync" or "SelectOptionAsync" or "HoverAsync"
            or "FocusAsync" or "PressAsync" or "DispatchEventAsync" or "WaitForSelectorAsync"
            or "WaitForNavigationAsync" or "WaitForURLAsync" or "WaitForLoadStateAsync" => true,
        _ => false,
    };

    private static bool HasCancellationTokenArgument(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation)
    {
        foreach (var arg in invocation.ArgumentList.Arguments)
        {
            var typeInfo = context.SemanticModel.GetTypeInfo(arg.Expression, context.CancellationToken);
            if (string.Equals(typeInfo.Type?.ToDisplayString(), "System.Threading.CancellationToken", StringComparison.Ordinal))
            {
                return true;
            }

            if (HasCancellationTokenInObjectInitializer(arg.Expression))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasCancellationTokenInObjectInitializer(ExpressionSyntax expression)
    {
        var initializer = expression switch
        {
            ObjectCreationExpressionSyntax objCreate => objCreate.Initializer,
            ImplicitObjectCreationExpressionSyntax implicitCreate => implicitCreate.Initializer,
            _ => null,
        };

        return initializer is not null
            && initializer.Expressions
                .OfType<AssignmentExpressionSyntax>()
                .Any(assign => assign.Left is IdentifierNameSyntax id
                    && string.Equals(id.Identifier.Text, "CancellationToken", StringComparison.Ordinal));
    }
}

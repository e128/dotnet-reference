using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128038: Flags <c lang="csharp">Task.WhenAll(collection.Select(async x => await httpClient.Method(url)))</c>
///     where the enclosing async method has a <see cref="System.Threading.CancellationToken" /> parameter
///     but that token is not forwarded to the HttpClient or Playwright method inside the lambda.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TaskWhenAllCancellationPropagationAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128038";

    private static readonly HashSet<string> HttpClientMethods = new(StringComparer.Ordinal)
    {
        "GetAsync",
        "PostAsync",
        "SendAsync",
        "PutAsync",
        "DeleteAsync",
        "PatchAsync",
        "GetStringAsync",
        "GetStreamAsync",
        "GetByteArrayAsync"
    };

    private static readonly HashSet<string> PlaywrightMethods = new(StringComparer.Ordinal)
    {
        "GotoAsync",
        "ClickAsync",
        "FillAsync",
        "TypeAsync",
        "TapAsync",
        "CheckAsync",
        "UncheckAsync",
        "SelectOptionAsync",
        "HoverAsync",
        "FocusAsync",
        "PressAsync",
        "DispatchEventAsync",
        "WaitForSelectorAsync",
        "WaitForNavigationAsync",
        "WaitForURLAsync",
        "WaitForLoadStateAsync"
    };

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Task.WhenAll async lambda missing CancellationToken propagation",
        "The async lambda inside Task.WhenAll calls an HttpClient or Playwright method without passing the enclosing CancellationToken",
        "E128-Reliability",
        DiagnosticSeverity.Warning,
        true,
        "When an enclosing async method has a CancellationToken parameter, " +
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
        context.RegisterCompilationStartAction(static compilationContext =>
        {
            var taskType = compilationContext.Compilation.GetTypeByMetadataName("System.Threading.Tasks.Task");
            if (taskType is null)
            {
                return;
            }

            compilationContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeInvocation(nodeContext, taskType),
                SyntaxKind.InvocationExpression);
        });
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol taskType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (!TaskWhenAllCalls.IsWhenAll(context, invocation, taskType))
        {
            return;
        }

        var args = invocation.ArgumentList.Arguments;
        if (args.Count is not 1)
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

        if (!hasUnpropagatedCall)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
    }

    private static bool TryGetSelectInvocation(ExpressionSyntax expression, [NotNullWhen(true)] out InvocationExpressionSyntax? selectInvocation)
    {
        selectInvocation = null;

        if (expression is not InvocationExpressionSyntax candidate)
        {
            return false;
        }

        if (candidate.Expression is not MemberAccessExpressionSyntax memberAccess
            || !string.Equals(memberAccess.Name.Identifier.ValueText, "Select", StringComparison.Ordinal))
        {
            return false;
        }

        selectInvocation = candidate;
        return true;
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
            _ => false
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

    private static bool IsHttpClientMethod(string name)
    {
        return HttpClientMethods.Contains(name);
    }

    private static bool IsPlaywrightMethod(string name)
    {
        return PlaywrightMethods.Contains(name);
    }

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
            _ => null
        };

        return initializer is not null
               && initializer.Expressions
                   .OfType<AssignmentExpressionSyntax>()
                   .Any(static assign => assign.Left is IdentifierNameSyntax id
                                  && string.Equals(id.Identifier.Text, "CancellationToken", StringComparison.Ordinal));
    }
}

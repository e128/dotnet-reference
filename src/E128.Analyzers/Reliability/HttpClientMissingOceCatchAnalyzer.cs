using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128051: Flags <see langword="async" /> methods that call an <c lang="csharp">HttpClient</c> method
///     and contain a broad <c lang="csharp">catch (Exception)</c> clause without a preceding
///     <c lang="csharp">catch (OperationCanceledException)</c> in the same try block.
///     Swallowing cancellation silently breaks cooperative cancellation.
/// </summary>
/// <remarks>
///     No code fix is provided — adding a catch clause with correct handler logic is too
///     context-dependent (rethrow vs log-and-rethrow vs propagate cancellation token).
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HttpClientMissingOceCatchAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128051";

    private const string HttpClientMetadataName = "System.Net.Http.HttpClient";
    private const string ExceptionMetadataName = "System.Exception";
    private const string OperationCanceledExceptionMetadataName = "System.OperationCanceledException";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Broad catch in async HttpClient method missing OperationCanceledException handler",
        "Broad catch (Exception) swallows OperationCanceledException — add catch (OperationCanceledException) before this catch to handle cancellation explicitly",
        "Reliability",
        DiagnosticSeverity.Warning,
        true,
        "An async method that calls HttpClient methods and uses a broad catch (Exception) " +
        "must also catch OperationCanceledException before the broad catch. " +
        "Without it, task cancellation is silently swallowed instead of propagated to the caller.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(startContext =>
        {
            // The rule names System.Net.Http types. Without them the method names below belong to
            // some other type, so the rule must not run at all.
            var compilation = startContext.Compilation;
            var httpClientType = compilation.GetTypeByMetadataName(HttpClientMetadataName);
            var exceptionType = compilation.GetTypeByMetadataName(ExceptionMetadataName);
            if (httpClientType is null || exceptionType is null)
            {
                return;
            }

            // TaskCanceledException derives from OperationCanceledException, so either catch counts.
            var operationCanceledType = compilation.GetTypeByMetadataName(OperationCanceledExceptionMetadataName);

            startContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeMethod(nodeContext, httpClientType, exceptionType, operationCanceledType),
                SyntaxKind.MethodDeclaration);
        });
    }

    private static void AnalyzeMethod(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol httpClientType,
        INamedTypeSymbol exceptionType,
        INamedTypeSymbol? operationCanceledType)
    {
        var method = (MethodDeclarationSyntax)context.Node;

        if (!method.Modifiers.Any(m => m.IsKind(SyntaxKind.AsyncKeyword)))
        {
            return;
        }

        if (method.Body is null)
        {
            return;
        }

        // One walk collects the HttpClient calls. Each try block then tests span containment
        // instead of re-walking its own subtree.
        var httpClientCalls = new List<InvocationExpressionSyntax>();
        foreach (var invocation in method.Body.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (IsHttpClientCall(context, invocation, httpClientType))
            {
                httpClientCalls.Add(invocation);
            }
        }

        if (httpClientCalls.Count == 0)
        {
            return;
        }

        foreach (var tryStatement in method.Body.DescendantNodes().OfType<TryStatementSyntax>())
        {
            if (!httpClientCalls.Exists(call => tryStatement.Block.Span.Contains(call.Span)))
            {
                continue;
            }

            for (var i = 0; i < tryStatement.Catches.Count; i++)
            {
                var catchClause = tryStatement.Catches[i];

                if (!IsBroadExceptionCatch(context, catchClause, exceptionType))
                {
                    continue;
                }

                var precedingCatches = tryStatement.Catches.Take(i);
                if (precedingCatches.Any(preceding => IsOperationCanceledExceptionCatch(context, preceding, operationCanceledType)))
                {
                    continue;
                }

                var span = TextSpan.FromBounds(
                    catchClause.CatchKeyword.SpanStart,
                    catchClause.Declaration?.CloseParenToken.Span.End ?? catchClause.CatchKeyword.Span.End);

                context.ReportDiagnostic(Diagnostic.Create(Rule, Location.Create(catchClause.SyntaxTree, span)));
            }
        }
    }

    private static bool IsHttpClientCall(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol httpClientType)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax { Name: IdentifierNameSyntax name })
        {
            return false;
        }

        if (!IsHttpClientMethod(name.Identifier.Text))
        {
            return false;
        }

        // A custom client type can declare GetAsync and PostAsync too. Only an HttpClient receiver
        // throws the cancellation this rule is about.
        return context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is IMethodSymbol method
               && TypeSymbols.DerivesFrom(method.ContainingType, httpClientType);
    }

    private static bool IsHttpClientMethod(string name)
    {
        return name switch
        {
            "GetAsync" or "PostAsync" or "SendAsync" or "PutAsync" or "DeleteAsync" => true,
            _ => false
        };
    }

    private static bool IsBroadExceptionCatch(
        SyntaxNodeAnalysisContext context,
        CatchClauseSyntax catchClause,
        INamedTypeSymbol exceptionType)
    {
        // A when-filtered catch is intentionally restricted — not considered broad
        if (catchClause.Filter is not null)
        {
            return false;
        }

        if (catchClause.Declaration is null)
        {
            return true; // bare catch { } catches everything
        }

        // Only System.Exception itself is broad. A derived type is a narrower catch, and a local
        // type that merely shares the name is not System.Exception at all.
        return SymbolEqualityComparer.Default.Equals(GetCatchType(context, catchClause), exceptionType);
    }

    private static bool IsOperationCanceledExceptionCatch(
        SyntaxNodeAnalysisContext context,
        CatchClauseSyntax catchClause,
        INamedTypeSymbol? operationCanceledType)
    {
        return operationCanceledType is not null
               && GetCatchType(context, catchClause) is INamedTypeSymbol type
               && TypeSymbols.DerivesFrom(type, operationCanceledType);
    }

    private static ITypeSymbol? GetCatchType(SyntaxNodeAnalysisContext context, CatchClauseSyntax catchClause)
    {
        return catchClause.Declaration is null
            ? null
            : context.SemanticModel.GetTypeInfo(catchClause.Declaration.Type, context.CancellationToken).Type;
    }

}

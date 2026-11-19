using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Security;

/// <summary>
///     E128241: One CORS policy builder chain calls both <c lang="csharp">AllowAnyOrigin</c> and
///     <c lang="csharp">AllowCredentials</c>.
/// </summary>
/// <remarks>
///     A browser rejects a credentialed request to a wildcard origin, so the policy cannot work
///     as written. It also states an intent to trust every origin with credentials. The rule
///     reports the chain once, at the <c lang="csharp">AllowAnyOrigin</c> call, in either order.
///     The rule resolves <c lang="csharp">CorsPolicyBuilder</c> and stays silent without an
///     ASP.NET Core reference. The remedy needs the allowed origin list, so the rule carries no
///     code fix.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CorsWildcardWithCredentialsAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128241";

    private const string BuilderTypeName = "Microsoft.AspNetCore.Cors.Infrastructure.CorsPolicyBuilder";

    private const string AllowAnyOriginName = "AllowAnyOrigin";

    private const string AllowCredentialsName = "AllowCredentials";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "CORS wildcard with credentials",
        "A CORS policy allows any origin together with credentials",
        "E128-Security",
        DiagnosticSeverity.Warning,
        true,
        "AllowAnyOrigin with AllowCredentials trusts every origin with credentialed requests, and a browser rejects the combination. List the trusted origins.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static start =>
        {
            if (start.Compilation.GetTypeByMetadataName(BuilderTypeName) is not { } builderType)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => Analyze(nodeContext, builderType), SyntaxKind.InvocationExpression);
        });
    }

    private static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol builderType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, builderType))
        {
            return;
        }

        if (string.Equals(method.Name, AllowAnyOriginName, StringComparison.Ordinal)
            && FindInChain(invocation, AllowCredentialsName, builderType, context) is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
            return;
        }

        if (!string.Equals(method.Name, AllowCredentialsName, StringComparison.Ordinal))
        {
            return;
        }

        if (FindInChain(invocation, AllowAnyOriginName, builderType, context) is not { } anyOrigin)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, anyOrigin.GetLocation()));
    }

    /// <summary>
    ///     Returns the earlier call in the receiver chain with the given name, or null. The walk
    ///     moves toward the receiver only, so a pair reports once at its later call.
    /// </summary>
    private static InvocationExpressionSyntax? FindInChain(
        InvocationExpressionSyntax invocation,
        string methodName,
        INamedTypeSymbol builderType,
        SyntaxNodeAnalysisContext context)
    {
        var current = invocation.Expression;
        while (current is MemberAccessExpressionSyntax access)
        {
            if (access.Expression is not InvocationExpressionSyntax previous)
            {
                return null;
            }

            if (context.SemanticModel.GetSymbolInfo(previous, context.CancellationToken).Symbol is IMethodSymbol method
                && SymbolEqualityComparer.Default.Equals(method.ContainingType, builderType)
                && string.Equals(method.Name, methodName, StringComparison.Ordinal))
            {
                return previous;
            }

            current = previous.Expression;
        }

        return null;
    }
}

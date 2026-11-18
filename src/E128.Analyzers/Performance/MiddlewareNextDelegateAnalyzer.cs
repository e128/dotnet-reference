using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

/// <summary>
///     E128229: An inline <c lang="csharp">Use</c> middleware takes a
///     <c lang="csharp">Func&lt;Task&gt;</c> next delegate, so every request allocates one closure.
/// </summary>
/// <remarks>
///     The <c lang="csharp">Func&lt;HttpContext, Func&lt;Task&gt;, Task&gt;</c> overload wraps the
///     next delegate in a new <c lang="csharp">Func&lt;Task&gt;</c> closure on every request. The
///     <c lang="csharp">Func&lt;HttpContext, RequestDelegate, Task&gt;</c> overload passes the same
///     <c lang="csharp">RequestDelegate</c> with no allocation. The rule reports the second
///     parameter only when the lambda calls <c lang="csharp">next()</c> with no argument and never
///     reassigns the context parameter, so the fix can rewrite both sites.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MiddlewareNextDelegateAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128229";

    private const string ApplicationBuilderTypeName = "Microsoft.AspNetCore.Builder.IApplicationBuilder";

    private const string UseMiddlewareExtensionsTypeName = "Microsoft.AspNetCore.Builder.UseMiddlewareExtensions";

    private const string HttpContextTypeName = "Microsoft.AspNetCore.Http.HttpContext";

    private const string RequestDelegateTypeName = "Microsoft.AspNetCore.Http.RequestDelegate";

    private const string TaskTypeName = "System.Threading.Tasks.Task";

    private const int MiddlewareParameterCount = 2;

    private const int MiddlewareDelegateArity = 3;

    private const int FuncOfTaskArity = 1;

    private const int ReturnTypeArgumentIndex = 2;

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Middleware binds the Func<Task> next delegate",
        "This overload allocates one Func<Task> closure per request. Use the RequestDelegate overload and call next(context).",
        "E128-Performance",
        DiagnosticSeverity.Warning,
        true,
        "The Func<HttpContext, RequestDelegate, Task> overload allocates no per-request closure. " +
        "Change the parameter type to RequestDelegate and pass the context to next.");

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
            var compilation = start.Compilation;
            var applicationBuilder = compilation.GetTypeByMetadataName(ApplicationBuilderTypeName);
            var useExtensions = compilation.GetTypeByMetadataName(UseMiddlewareExtensionsTypeName);
            var httpContext = compilation.GetTypeByMetadataName(HttpContextTypeName);
            var requestDelegate = compilation.GetTypeByMetadataName(RequestDelegateTypeName);
            var task = compilation.GetTypeByMetadataName(TaskTypeName);
            var func1 = compilation.GetTypeByMetadataName("System.Func`1");
            var func3 = compilation.GetTypeByMetadataName("System.Func`3");

            if (applicationBuilder is null
                || httpContext is null
                || requestDelegate is null
                || task is null
                || func1 is null
                || func3 is null)
            {
                return;
            }

            var middleware = new MiddlewareContext(applicationBuilder, useExtensions, httpContext, func1, func3, task);

            start.RegisterSyntaxNodeAction(nodeContext => Analyze(nodeContext, middleware), SyntaxKind.InvocationExpression);
        });
    }

    private static void Analyze(SyntaxNodeAnalysisContext context, MiddlewareContext middleware)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (FindMiddlewareLambda(invocation, context, middleware) is not { } lambda
            || !TryGetParameterNames(lambda, out var contextName, out var nextName, out var nextType)
            || nextType is null
            || !CallsNextWithNoArgument(lambda.Body, contextName, nextName))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, nextType.GetLocation()));
    }

    private static ParenthesizedLambdaExpressionSyntax? FindMiddlewareLambda(
        InvocationExpressionSyntax invocation,
        SyntaxNodeAnalysisContext context,
        MiddlewareContext middleware)
    {
        if (invocation.ArgumentList.Arguments.Count is not 1
            || invocation.ArgumentList.Arguments[0].Expression is not ParenthesizedLambdaExpressionSyntax lambda
            || lambda.ParameterList.Parameters.Count is not MiddlewareParameterCount)
        {
            return null;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !IsApplicationBuilderUse(method, middleware)
            || method.Parameters.Length is not 1
            || method.Parameters[0].Type is not INamedTypeSymbol delegateType
            || !IsFuncTaskDelegate(delegateType, middleware))
        {
            return null;
        }

        return lambda;
    }

    private static bool TryGetParameterNames(
        ParenthesizedLambdaExpressionSyntax lambda,
        out string contextName,
        out string nextName,
        out TypeSyntax? nextType)
    {
        var nextParameter = lambda.ParameterList.Parameters[1];
        nextType = nextParameter.Type;
        contextName = lambda.ParameterList.Parameters[0].Identifier.ValueText;
        nextName = nextParameter.Identifier.ValueText;

        return nextType is not null
               && !string.IsNullOrEmpty(contextName)
               && !string.IsNullOrEmpty(nextName)
               && !string.Equals(contextName, "_", StringComparison.Ordinal);
    }

    private static bool IsApplicationBuilderUse(IMethodSymbol method, MiddlewareContext middleware)
    {
        return string.Equals(method.Name, "Use", StringComparison.Ordinal)
               && (TypeSymbols.Matches(method.ContainingType, middleware.ApplicationBuilder)
                   || TypeSymbols.Matches(method.ContainingType, middleware.UseExtensions));
    }

    private static bool IsFuncTaskDelegate(INamedTypeSymbol delegateType, MiddlewareContext middleware)
    {
        return TypeSymbols.MatchesOriginalDefinition(delegateType, middleware.Func3)
               && delegateType.TypeArguments.Length is MiddlewareDelegateArity
               && SymbolEqualityComparer.Default.Equals(delegateType.TypeArguments[0], middleware.HttpContext)
               && IsFuncTask(delegateType.TypeArguments[1], middleware)
               && SymbolEqualityComparer.Default.Equals(
                   delegateType.TypeArguments[ReturnTypeArgumentIndex],
                   middleware.Task);
    }

    private static bool IsFuncTask(ITypeSymbol? type, MiddlewareContext middleware)
    {
        return type is INamedTypeSymbol { TypeArguments.Length: FuncOfTaskArity } named
               && TypeSymbols.MatchesOriginalDefinition(named, middleware.Func1)
               && SymbolEqualityComparer.Default.Equals(named.TypeArguments[0], middleware.Task);
    }

    /// <summary>
    ///     True when every use of the next delegate is a call with no argument, at least one such
    ///     call exists, and the context parameter carries no assignment.
    /// </summary>
    internal static bool CallsNextWithNoArgument(SyntaxNode body, string contextName, string nextName)
    {
        var calls = 0;

        foreach (var node in body.DescendantNodes())
        {
            if (node is not IdentifierNameSyntax identifier)
            {
                continue;
            }

            if (IsNamed(identifier, contextName))
            {
                if (IsWriteTarget(identifier))
                {
                    return false;
                }

                continue;
            }

            if (!IsNamed(identifier, nextName))
            {
                continue;
            }

            if (identifier.Parent is InvocationExpressionSyntax call
                && call.Expression == identifier
                && call.ArgumentList.Arguments.Count is 0)
            {
                calls++;
                continue;
            }

            return false;
        }

        return calls > 0;
    }

    internal static bool IsWriteTarget(IdentifierNameSyntax identifier)
    {
        return identifier.Parent switch
        {
            AssignmentExpressionSyntax assignment => assignment.Left == identifier,
            PrefixUnaryExpressionSyntax prefix => prefix.Operand == identifier
                                                   && (prefix.IsKind(SyntaxKind.PreIncrementExpression)
                                                       || prefix.IsKind(SyntaxKind.PreDecrementExpression)),
            PostfixUnaryExpressionSyntax postfix => postfix.Operand == identifier,
            ArgumentSyntax argument => argument.RefKindKeyword.IsKind(SyntaxKind.RefKeyword)
                                       || argument.RefKindKeyword.IsKind(SyntaxKind.OutKeyword),
            _ => false
        };
    }

    private static bool IsNamed(IdentifierNameSyntax identifier, string name)
    {
        return string.Equals(identifier.Identifier.ValueText, name, StringComparison.Ordinal);
    }

    internal sealed class MiddlewareContext
    {
        public MiddlewareContext(
            INamedTypeSymbol applicationBuilder,
            INamedTypeSymbol? useExtensions,
            INamedTypeSymbol httpContext,
            INamedTypeSymbol func1,
            INamedTypeSymbol func3,
            INamedTypeSymbol task)
        {
            ApplicationBuilder = applicationBuilder;
            UseExtensions = useExtensions;
            HttpContext = httpContext;
            Func1 = func1;
            Func3 = func3;
            Task = task;
        }

        public INamedTypeSymbol ApplicationBuilder { get; }

        public INamedTypeSymbol? UseExtensions { get; }

        public INamedTypeSymbol HttpContext { get; }

        public INamedTypeSymbol Func1 { get; }

        public INamedTypeSymbol Func3 { get; }

        public INamedTypeSymbol Task { get; }
    }
}

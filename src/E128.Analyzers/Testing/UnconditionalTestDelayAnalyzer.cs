using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Testing;

/// <summary>
///     E128236: Flags a delay call inside an xUnit <c lang="csharp">[Fact]</c> or <c lang="csharp">[Theory]</c> that
///     waits a fixed time with no way to cancel. The rule reports <c lang="csharp">Thread.Sleep</c>,
///     <c lang="csharp">SpinWait.SpinUntil</c>, and <c lang="csharp">Task.Delay</c> with a literal duration and no
///     CancellationToken.
/// </summary>
/// <remarks>
///     <para>
///         A delay inside a loop stays silent, because E128211 owns the loop case. A computed or variable
///         duration stays silent, because the test may have chosen it on purpose. A delay that carries a
///         CancellationToken stays silent, because the caller can cancel it.
///     </para>
///     <para>
///         The rule reports no code fix. The remedy needs a real synchronization signal that only the
///         application intent can name.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnconditionalTestDelayAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128236";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "A test waits a fixed delay instead of a signal",
        "This test waits a fixed delay with no cancellation: wait for the signal that the code under test raises, with a timeout and a token",
        "E128-Testing",
        DiagnosticSeverity.Warning,
        true,
        "A fixed delay in a test wastes CI time and still flakes when a machine runs slow. Wait for the signal that the code under test raises. Pass a timeout and a CancellationToken, or use a polling helper that observes cancellation. E128211 covers a delay inside a loop.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var delayTypes = new DelayTypes(
            context.Compilation.GetTypeByMetadataName("System.Threading.Thread"),
            context.Compilation.GetTypeByMetadataName("System.Threading.SpinWait"),
            context.Compilation.GetTypeByMetadataName("System.Threading.Tasks.Task"),
            context.Compilation.GetTypeByMetadataName("System.Threading.CancellationToken"),
            context.Compilation.GetTypeByMetadataName("System.TimeSpan"));

        if (delayTypes.IsEmpty)
        {
            return;
        }

        context.RegisterSyntaxNodeAction(
            nodeContext => AnalyzeMethod(nodeContext, delayTypes),
            SyntaxKind.MethodDeclaration);
    }

    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context, DelayTypes types)
    {
        var method = (MethodDeclarationSyntax)context.Node;

        // An editor buffer can hold a partial method with no body, so the guard reads the method
        // shape before the rule reads the symbol.
        var scope = (SyntaxNode?)method.Body ?? method.ExpressionBody?.Expression;

        if (scope is null)
        {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(method, context.CancellationToken) is not IMethodSymbol symbol
            || !XunitTestHelper.IsXunitTestMethod(symbol))
        {
            return;
        }

        foreach (var invocation in scope.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (IsReportedCall(invocation, types, context))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
            }
        }
    }

    private static bool IsReportedCall(
        InvocationExpressionSyntax invocation,
        DelayTypes types,
        SyntaxNodeAnalysisContext context)
    {
        // Bind late. The name gate rejects only calls that the symbol check also rejects.
        var calledName = InvocationNames.GetSimpleName(invocation)?.Identifier.ValueText;

        if (calledName is null || !IsDelayName(calledName))
        {
            return false;
        }

        // A delay inside a loop stays silent, because E128211 owns the loop case.
        if (LoopContext.IsWithinAnyLoop(invocation))
        {
            return false;
        }

        return context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
               is IMethodSymbol called
               && IsReportedDelay(invocation, called, types, context);
    }

    private static bool IsDelayName(string name)
    {
        return string.Equals(name, "Sleep", StringComparison.Ordinal)
               || string.Equals(name, "SpinUntil", StringComparison.Ordinal)
               || string.Equals(name, "Delay", StringComparison.Ordinal);
    }

    private static bool IsReportedDelay(
        InvocationExpressionSyntax invocation,
        IMethodSymbol called,
        DelayTypes types,
        SyntaxNodeAnalysisContext context)
    {
        return IsThreadSleep(called, types.Thread)
               || IsSpinWaitUntil(called, types.SpinWait)
               || IsUnconditionalTaskDelay(invocation, called, types, context);
    }

    private static bool IsThreadSleep(IMethodSymbol called, INamedTypeSymbol? thread)
    {
        return string.Equals(called.Name, "Sleep", StringComparison.Ordinal)
               && SymbolEqualityComparer.Default.Equals(called.ContainingType, thread);
    }

    private static bool IsSpinWaitUntil(IMethodSymbol called, INamedTypeSymbol? spinWait)
    {
        return string.Equals(called.Name, "SpinUntil", StringComparison.Ordinal)
               && SymbolEqualityComparer.Default.Equals(called.ContainingType, spinWait);
    }

    // A missing CancellationToken symbol cannot prove the token absence, so the Task branch needs
    // the type before it can report.
    private static bool IsUnconditionalTaskDelay(
        InvocationExpressionSyntax invocation,
        IMethodSymbol called,
        DelayTypes types,
        SyntaxNodeAnalysisContext context)
    {
        return types.CancellationToken is not null
               && SymbolEqualityComparer.Default.Equals(called.ContainingType, types.Task)
               && string.Equals(called.Name, "Delay", StringComparison.Ordinal)
               && !TakesCancellationToken(called, types)
               && IsConstantDuration(invocation, types, context);
    }

    private static bool TakesCancellationToken(IMethodSymbol called, DelayTypes types)
    {
        return called.Parameters.Any(
            parameter => SymbolEqualityComparer.Default.Equals(parameter.Type, types.CancellationToken));
    }

    // A variable or computed duration can encode a real timeout the test chose, so the rule reads only
    // a literal integer and a TimeSpan factory call that one literal argument builds.
    private static bool IsConstantDuration(
        InvocationExpressionSyntax invocation,
        DelayTypes types,
        SyntaxNodeAnalysisContext context)
    {
        if (invocation.ArgumentList is not { Arguments: { Count: > 0 } arguments })
        {
            return false;
        }

        var duration = arguments[0].Expression;

        if (duration is LiteralExpressionSyntax literal)
        {
            return literal.IsKind(SyntaxKind.NumericLiteralExpression);
        }

        return duration is InvocationExpressionSyntax factory
               && factory.ArgumentList is { Arguments.Count: 1 }
               && factory.ArgumentList.Arguments[0].Expression is LiteralExpressionSyntax factoryArgument
               && factoryArgument.IsKind(SyntaxKind.NumericLiteralExpression)
               && context.SemanticModel.GetSymbolInfo(factory, context.CancellationToken).Symbol
                   is IMethodSymbol factoryMethod
               && SymbolEqualityComparer.Default.Equals(factoryMethod.ContainingType, types.TimeSpan)
               && IsTimeSpanFactoryName(factoryMethod.Name);
    }

    private static bool IsTimeSpanFactoryName(string name)
    {
        return string.Equals(name, "FromMilliseconds", StringComparison.Ordinal)
               || string.Equals(name, "FromSeconds", StringComparison.Ordinal)
               || string.Equals(name, "FromMinutes", StringComparison.Ordinal)
               || string.Equals(name, "FromHours", StringComparison.Ordinal)
               || string.Equals(name, "FromDays", StringComparison.Ordinal);
    }

    private sealed class DelayTypes
    {
        internal DelayTypes(
            INamedTypeSymbol? thread,
            INamedTypeSymbol? spinWait,
            INamedTypeSymbol? task,
            INamedTypeSymbol? cancellationToken,
            INamedTypeSymbol? timeSpan)
        {
            Thread = thread;
            SpinWait = spinWait;
            Task = task;
            CancellationToken = cancellationToken;
            TimeSpan = timeSpan;
        }

        internal bool IsEmpty => Thread is null && SpinWait is null && Task is null;

        internal INamedTypeSymbol? Thread { get; }

        internal INamedTypeSymbol? SpinWait { get; }

        internal INamedTypeSymbol? Task { get; }

        internal INamedTypeSymbol? CancellationToken { get; }

        internal INamedTypeSymbol? TimeSpan { get; }
    }
}

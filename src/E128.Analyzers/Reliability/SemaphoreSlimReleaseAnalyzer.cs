using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128175: Reports a <c lang="csharp">SemaphoreSlim.Wait</c> or
///     <c lang="csharp">SemaphoreSlim.WaitAsync</c> call that no later
///     <see langword="try" />/<see langword="finally" /> pairs with a <c lang="csharp">Release</c> call.
///     An exception between the wait and the release leaves the permit held.
/// </summary>
/// <remarks>
///     The rule reads the plain overloads only. A timeout overload supports the try-acquire shape,
///     where the wait sits inside an <see langword="if" /> and no permit is held on the false path.
///     The rule also stays silent when an enclosing <see langword="try" /> already releases, so it
///     does not report the separate over-release shape.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SemaphoreSlimReleaseAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128175";

    internal const string SemaphoreTypeName = "System.Threading.SemaphoreSlim";

    internal const string CancellationTokenTypeName = "System.Threading.CancellationToken";

    private const string WaitName = "Wait";

    private const string WaitAsyncName = "WaitAsync";

    private const string ReleaseName = "Release";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "SemaphoreSlim wait without a release guarantee",
        "SemaphoreSlim.{0} has no release guarantee: an exception between the wait and the release leaks the permit",
        "E128-Reliability",
        DiagnosticSeverity.Warning,
        true,
        "A SemaphoreSlim.Wait or WaitAsync call that no later try/finally pairs with a Release call " +
        "leaks the permit when the code that follows the wait throws. Put the wait before a try " +
        "block whose finally calls Release.");

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
        var semaphores = MetadataTypes.Find(context.Compilation, SemaphoreTypeName);
        var cancellationTokenType = context.Compilation.GetTypeByMetadataName(CancellationTokenTypeName);

        if (semaphores.IsDefaultOrEmpty || cancellationTokenType is null)
        {
            return;
        }

        context.RegisterSyntaxNodeAction(
            ctx => AnalyzeInvocation(ctx, semaphores, cancellationTokenType),
            SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(
        SyntaxNodeAnalysisContext context,
        ImmutableArray<INamedTypeSymbol> semaphores,
        INamedTypeSymbol cancellationTokenType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        var model = context.SemanticModel;
        var token = context.CancellationToken;

        if (model.GetSymbolInfo(invocation, token).Symbol is not IMethodSymbol method)
        {
            return;
        }

        if (!IsWaitCall(method, semaphores, cancellationTokenType))
        {
            return;
        }

        var receiver = model.GetSymbolInfo(memberAccess.Expression, token).Symbol;

        if (receiver is null)
        {
            // The buffer does not compile, so the rule cannot name the storage to pair.
            return;
        }

        if (HasEnclosingRelease(invocation, receiver, model, token)
            || HasFollowingRelease(invocation, receiver, model, token))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), method.Name));
    }

    private static bool IsWaitCall(
        IMethodSymbol method,
        ImmutableArray<INamedTypeSymbol> semaphores,
        INamedTypeSymbol cancellationTokenType)
    {
        var isNamedWait = string.Equals(method.Name, WaitName, StringComparison.Ordinal)
                          || string.Equals(method.Name, WaitAsyncName, StringComparison.Ordinal);

        var takesPlainOrTokenOnly = method.Parameters.Length == 0
                                    || (method.Parameters.Length == 1
                                        && SymbolEqualityComparer.Default.Equals(
                                            method.Parameters[0].Type, cancellationTokenType));

        return isNamedWait && takesPlainOrTokenOnly && semaphores.Any(semaphore => TypeSymbols.DerivesFrom(method.ContainingType, semaphore));
    }

    /// <summary>
    ///     True when a <see langword="try" /> outside the wait owns the release. This covers the
    ///     wait-inside-try shape, which raises a different defect, so the rule stays silent.
    /// </summary>
    private static bool HasEnclosingRelease(
        InvocationExpressionSyntax wait,
        ISymbol receiver,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        for (var node = wait.Parent; node is not null; node = node.Parent)
        {
            if (node is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax)
            {
                return false;
            }

            if (node is TryStatementSyntax tryStatement
                && ReleasesInFinally(tryStatement, receiver, model, cancellationToken))
            {
                return true;
            }

            if (node is BaseMethodDeclarationSyntax or AccessorDeclarationSyntax)
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>
    ///     True when a statement after the wait is a <see langword="try" /> whose
    ///     <see langword="finally" /> releases the same semaphore. A return, throw, break, or continue
    ///     before that point ends the search.
    /// </summary>
    private static bool HasFollowingRelease(
        InvocationExpressionSyntax wait,
        ISymbol receiver,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        if (wait.FirstAncestorOrSelf<ExpressionStatementSyntax>() is not { Parent: BlockSyntax block } statement)
        {
            return false;
        }

        for (var node = wait.Parent; node is not null && node != statement; node = node.Parent)
        {
            if (node is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax)
            {
                return false;
            }
        }

        var statements = block.Statements;
        var start = statements.IndexOf(statement);

        if (start < 0)
        {
            return false;
        }

        for (var i = start + 1; i < statements.Count; i++)
        {
            var next = statements[i];

            if (next is TryStatementSyntax tryStatement
                && ReleasesInFinally(tryStatement, receiver, model, cancellationToken))
            {
                return true;
            }

            if (CanLeaveTheBlock(next))
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>
    ///     True when the statement can leave the enclosing block, so a later release never runs. A
    ///     break or a continue exits only an inner loop, so neither counts. A continue in a loop body
    ///     that skips a later release is a known gap.
    /// </summary>
    private static bool CanLeaveTheBlock(SyntaxNode statement)
    {
        foreach (var child in statement.ChildNodes())
        {
            if (child is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax)
            {
                continue;
            }

            if (child is ReturnStatementSyntax or ThrowStatementSyntax or GotoStatementSyntax
                or YieldStatementSyntax)
            {
                return true;
            }

            if (CanLeaveTheBlock(child))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ReleasesInFinally(
        TryStatementSyntax tryStatement,
        ISymbol receiver,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        if (tryStatement.Finally?.Block is not { } finallyBlock)
        {
            return false;
        }

        foreach (var invocation in finallyBlock.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            {
                continue;
            }

            if (!string.Equals(memberAccess.Name.Identifier.ValueText, ReleaseName, StringComparison.Ordinal))
            {
                continue;
            }

            if (model.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol release
                || !string.Equals(release.Name, ReleaseName, StringComparison.Ordinal))
            {
                continue;
            }

            var releaseReceiver = model.GetSymbolInfo(memberAccess.Expression, cancellationToken).Symbol;

            if (releaseReceiver is not null && SymbolEqualityComparer.Default.Equals(receiver, releaseReceiver))
            {
                return true;
            }
        }

        return false;
    }
}

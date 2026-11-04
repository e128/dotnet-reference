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
///     E128160: Reports a process that waits for exit while one of its streams is redirected and
///     never read. The child blocks once the pipe buffer is full, and the wait never returns.
/// </summary>
/// <remarks>
///     The rule reads one method body. A drain through a helper method stays silent, because the
///     call does not appear where the wait happens.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ProcessOutputDrainAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128160";

    private const string ProcessTypeName = "System.Diagnostics.Process";

    private const string StartInfoTypeName = "System.Diagnostics.ProcessStartInfo";

    private static readonly ImmutableHashSet<string> RedirectProperties = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "RedirectStandardOutput",
        "RedirectStandardError");

    private static readonly ImmutableHashSet<string> DrainMethodNames = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "BeginErrorReadLine",
        "BeginOutputReadLine",
        "ReadLine",
        "ReadLineAsync",
        "ReadToEnd",
        "ReadToEndAsync");

    private static readonly ImmutableHashSet<string> DrainEventNames = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "ErrorDataReceived",
        "OutputDataReceived");

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Redirected process output never read before WaitForExit",
        "Read the redirected output before WaitForExit",
        "E128-Reliability",
        DiagnosticSeverity.Warning,
        true,
        "A process with a redirected stream writes into a pipe with a fixed buffer. When the " +
        "buffer fills, the child blocks on the write and WaitForExit never returns. Read the " +
        "stream, or attach an event handler, before the wait.");

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

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess
            || !string.Equals(memberAccess.Name.Identifier.ValueText, "WaitForExit", StringComparison.Ordinal))
        {
            return;
        }

        var model = context.SemanticModel;
        var cancellationToken = context.CancellationToken;

        if (model.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol method
            || !string.Equals(
                method.ContainingType?.ToDisplayString(),
                ProcessTypeName,
                StringComparison.Ordinal)
            || GetRootSymbol(memberAccess.Expression, model, cancellationToken) is not { } processSymbol)
        {
            return;
        }

        if (GetScope(invocation) is not { } scope)
        {
            return;
        }

        if (!HasRedirectedStream(scope, processSymbol, model, cancellationToken)
            || HasDrainBefore(scope, processSymbol, model, invocation.SpanStart, cancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
    }

    /// <summary>
    ///     Resolves the leftmost receiver of a member chain, so <c lang="csharp">process</c>,
    ///     <c lang="csharp">process.StartInfo</c>, and <c lang="csharp">process.StandardOutput</c>
    ///     all resolve to the same process symbol.
    /// </summary>
    private static ISymbol? GetRootSymbol(
        ExpressionSyntax expression,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        var current = expression;

        while (current is MemberAccessExpressionSyntax memberAccess)
        {
            current = memberAccess.Expression;
        }

        return model.GetSymbolInfo(current, cancellationToken).Symbol;
    }

    private static SyntaxNode? GetScope(InvocationExpressionSyntax invocation)
    {
        // The configuration and the wait usually sit in one method. A lambda body is the scope for a
        // wait inside a callback, because the enclosing method holds no relation to the process.
        return invocation.FirstAncestorOrSelf<AnonymousFunctionExpressionSyntax>() is { } lambda
            ? lambda.Body
            : invocation.FirstAncestorOrSelf<BaseMethodDeclarationSyntax>() is { } method
                ? (SyntaxNode?)method.Body ?? method.ExpressionBody
                : invocation.FirstAncestorOrSelf<BlockSyntax>();
    }

    private static bool HasRedirectedStream(
        SyntaxNode scope,
        ISymbol processSymbol,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        return scope.DescendantNodes()
            .OfType<AssignmentExpressionSyntax>()
            .Any(assignment => IsTrueRedirect(assignment, processSymbol, model, cancellationToken));
    }

    private static bool IsTrueRedirect(
        AssignmentExpressionSyntax assignment,
        ISymbol processSymbol,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        if (!assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
            || assignment.Right is not LiteralExpressionSyntax literal
            || !literal.IsKind(SyntaxKind.TrueLiteralExpression))
        {
            return false;
        }

        if (!TryGetRedirectTarget(assignment.Left, out var name, out var receiver)
            || !RedirectProperties.Contains(name)
            || model.GetSymbolInfo(assignment.Left, cancellationToken).Symbol is not IPropertySymbol property
            || !string.Equals(
                property.ContainingType?.ToDisplayString(),
                StartInfoTypeName,
                StringComparison.Ordinal))
        {
            return false;
        }

        // An object initializer has no receiver, so the flag belongs to the info instance the
        // process is started with.
        return receiver is null
               || SymbolEqualityComparer.Default.Equals(
                   GetRootSymbol(receiver, model, cancellationToken),
                   processSymbol);
    }

    /// <summary>
    ///     Splits an assignment target into the property name and the receiver. An object initializer
    ///     binds a bare identifier, so its receiver is absent.
    /// </summary>
    private static bool TryGetRedirectTarget(
        ExpressionSyntax left,
        out string name,
        out ExpressionSyntax? receiver)
    {
        switch (left)
        {
            case MemberAccessExpressionSyntax access:
                name = access.Name.Identifier.ValueText;
                receiver = access.Expression;
                return true;
            case IdentifierNameSyntax identifier:
                name = identifier.Identifier.ValueText;
                receiver = null;
                return true;
            default:
                name = string.Empty;
                receiver = null;
                return false;
        }
    }

    /// <summary>
    ///     True when a drain on the same process runs before the wait. A read placed after the wait
    ///     cannot unblock the child, and a read on another process cannot drain this one.
    /// </summary>
    private static bool HasDrainBefore(
        SyntaxNode scope,
        ISymbol processSymbol,
        SemanticModel model,
        int waitPosition,
        CancellationToken cancellationToken)
    {
        foreach (var node in scope.DescendantNodes())
        {
            if (node.SpanStart >= waitPosition)
            {
                continue;
            }

            var isDrain = node switch
            {
                InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access } =>
                    DrainMethodNames.Contains(access.Name.Identifier.ValueText)
                    && SymbolEqualityComparer.Default.Equals(
                        GetRootSymbol(access.Expression, model, cancellationToken),
                        processSymbol),
                AssignmentExpressionSyntax subscription =>
                    subscription.IsKind(SyntaxKind.AddAssignmentExpression)
                    && subscription.Left is MemberAccessExpressionSyntax left
                    && DrainEventNames.Contains(left.Name.Identifier.ValueText)
                    && SymbolEqualityComparer.Default.Equals(
                        GetRootSymbol(left.Expression, model, cancellationToken),
                        processSymbol),
                _ => false
            };

            if (isDrain)
            {
                return true;
            }
        }

        return false;
    }
}

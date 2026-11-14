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
///     E128223: Reports a process launch that redirects a standard stream, reads the stream with
///     <c lang="csharp">ReadToEnd</c> or <c lang="csharp">ReadToEndAsync</c>, and then waits for
///     exit. <c lang="csharp">Process.RunAndCaptureText</c> and its async form return the standard
///     output, the standard error, and the exit status in one call.
/// </summary>
/// <remarks>
///     The rule reads one method body. A launch that redirects standard input is out of scope,
///     because the caller interacts with the child. A line read is out of scope, because the
///     caller stops early. The rule reports no fix, because the replacement changes the error
///     handling around the launch.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HandRolledProcessCaptureAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128223";

    private const string ProcessTypeName = "System.Diagnostics.Process";

    private const string StartInfoTypeName = "System.Diagnostics.ProcessStartInfo";

    private const string StartInfoMemberName = "StartInfo";

    private const string StandardInputFlagName = "RedirectStandardInput";

    private static readonly ImmutableHashSet<string> CaptureFlagNames = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "RedirectStandardOutput",
        "RedirectStandardError");

    private static readonly ImmutableHashSet<string> CaptureReadNames = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "ReadToEnd",
        "ReadToEndAsync");

    private static readonly ImmutableHashSet<string> StandardStreamNames = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "StandardOutput",
        "StandardError");

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Hand-rolled child-process capture",
        "Process '{0}' redirects a standard stream, reads it, then waits for exit. Use Process.RunAndCaptureText to get the standard output, the standard error, and the exit status in one call.",
        "E128-Reliability",
        DiagnosticSeverity.Warning,
        true);

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
        context.RegisterSyntaxNodeAction(AnalyzeWait, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeWait(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax wait
            || wait.Name.Identifier.ValueText is not ("WaitForExit" or "WaitForExitAsync"))
        {
            return;
        }

        var model = context.SemanticModel;
        var cancellationToken = context.CancellationToken;

        if (!IsProcessWait(invocation, wait, model, cancellationToken)
            || ProcessLaunchScope.GetRootSymbol(wait.Expression, model, cancellationToken) is not { } processSymbol
            || ProcessLaunchScope.GetScope(invocation) is not { } scope)
        {
            return;
        }

        if (!CaptureFlagNames.Any(name => HasTrueStartInfoFlag(scope, processSymbol, name, model, cancellationToken))
            || HasTrueStartInfoFlag(scope, processSymbol, StandardInputFlagName, model, cancellationToken)
            || !HasCaptureReadBefore(scope, processSymbol, model, invocation.SpanStart, cancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), processSymbol.Name));
    }

    private static bool IsProcessWait(
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax wait,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        return model.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol method
               && string.Equals(method.ContainingType?.ToDisplayString(), ProcessTypeName, StringComparison.Ordinal)
               && ProcessLaunchScope.GetRootSymbol(wait.Expression, model, cancellationToken) is not null;
    }

    /// <summary>
    ///     True when a <c lang="csharp">ProcessStartInfo</c> property named
    ///     <paramref name="propertyName" /> is set to <see langword="true" /> on the start info that
    ///     belongs to <paramref name="processSymbol" />.
    /// </summary>
    private static bool HasTrueStartInfoFlag(
        SyntaxNode scope,
        ISymbol processSymbol,
        string propertyName,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        foreach (var assignment in scope.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            if (IsTrueStartInfoFlag(assignment, propertyName, model, cancellationToken)
                && FlagBinds(scope, assignment, processSymbol, model, cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsTrueStartInfoFlag(
        AssignmentExpressionSyntax assignment,
        string propertyName,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        return assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
               && assignment.Right is LiteralExpressionSyntax literal
               && literal.IsKind(SyntaxKind.TrueLiteralExpression)
               && TryGetTarget(assignment.Left, out var name, out _)
               && string.Equals(name, propertyName, StringComparison.Ordinal)
               && model.GetSymbolInfo(assignment.Left, cancellationToken).Symbol is IPropertySymbol property
               && string.Equals(property.ContainingType?.ToDisplayString(), StartInfoTypeName, StringComparison.Ordinal);
    }

    private static bool FlagBinds(
        SyntaxNode scope,
        AssignmentExpressionSyntax assignment,
        ISymbol processSymbol,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        if (TryGetTarget(assignment.Left, out _, out var receiver) && receiver is not null)
        {
            return SymbolEqualityComparer.Default.Equals(
                       ProcessLaunchScope.GetRootSymbol(receiver, model, cancellationToken),
                       processSymbol)
                   || StartInfoReceiverBinds(scope, receiver, processSymbol, model, cancellationToken);
        }

        return InitializerBindsToProcess(scope, assignment, processSymbol, model, cancellationToken);
    }

    /// <summary>
    ///     True when the receiver is a start info local that reaches the process, by a
    ///     <c lang="csharp">Process.Start</c> call or by an assignment to the process start info
    ///     member. The flag is set after creation, so the local itself carries the binding.
    /// </summary>
    private static bool StartInfoReceiverBinds(
        SyntaxNode scope,
        ExpressionSyntax receiver,
        ISymbol processSymbol,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        return ProcessLaunchScope.GetRootSymbol(receiver, model, cancellationToken) is { } local
               && LocalLinksToProcess(scope, local, processSymbol, model, cancellationToken);
    }

    private static bool InitializerBindsToProcess(
        SyntaxNode scope,
        AssignmentExpressionSyntax assignment,
        ISymbol processSymbol,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        // An object initializer has no receiver. The flag belongs to the start info instance the
        // initializer builds, and that instance must reach the process.
        return assignment.Parent is InitializerExpressionSyntax { Parent: BaseObjectCreationExpressionSyntax creation }
               && creation.Parent is { } parent
               && IsStartInfoCreation(creation, model, cancellationToken)
               && StartInfoCreationBinds(scope, creation, parent, processSymbol, model, cancellationToken);
    }

    /// <summary>
    ///     True when the start info creation reaches the process. It is passed to
    ///     <c lang="csharp">Process.Start</c>, stored in a local that reaches the process, assigned
    ///     to the process start info member, or set in the process object initializer.
    /// </summary>
    private static bool StartInfoCreationBinds(
        SyntaxNode scope,
        BaseObjectCreationExpressionSyntax creation,
        SyntaxNode parent,
        ISymbol processSymbol,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        switch (parent)
        {
            case ArgumentSyntax { Parent: ArgumentListSyntax { Parent: InvocationExpressionSyntax start } }:
                return IsProcessStart(start, model, cancellationToken)
                       && FlowsTo(start, processSymbol, model, cancellationToken);

            case EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator }:
                return model.GetDeclaredSymbol(declarator, cancellationToken) is { } local
                       && LocalLinksToProcess(scope, local, processSymbol, model, cancellationToken);

            case AssignmentExpressionSyntax assignment when ProcessLaunchScope.Unwrap(assignment.Right) == creation:
                return AssignmentTargetBinds(assignment, processSymbol, model, cancellationToken);

            case ParenthesizedExpressionSyntax or PostfixUnaryExpressionSyntax:
                return parent.Parent is { } outer
                       && StartInfoCreationBinds(scope, creation, outer, processSymbol, model, cancellationToken);

            default:
                return false;
        }
    }

    /// <summary>
    ///     True when the assignment target is the process start info member or the start info entry
    ///     of a process object initializer.
    /// </summary>
    private static bool AssignmentTargetBinds(
        AssignmentExpressionSyntax assignment,
        ISymbol processSymbol,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        if (assignment.Parent is InitializerExpressionSyntax { Parent: BaseObjectCreationExpressionSyntax owner }
            && assignment.Left is IdentifierNameSyntax startInfoName
            && IsStartInfoName(startInfoName))
        {
            return FlowsTo(owner, processSymbol, model, cancellationToken);
        }

        return assignment.Left is MemberAccessExpressionSyntax access
               && IsStartInfoName(access.Name)
               && SymbolEqualityComparer.Default.Equals(
                   ProcessLaunchScope.GetRootSymbol(access.Expression, model, cancellationToken),
                   processSymbol);
    }

    /// <summary>
    ///     True when the start info local reaches the process, by a <c lang="csharp">Process.Start</c>
    ///     call or by an assignment to the process start info member.
    /// </summary>
    private static bool LocalLinksToProcess(
        SyntaxNode scope,
        ISymbol local,
        ISymbol processSymbol,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        foreach (var node in scope.DescendantNodes())
        {
            if (node is InvocationExpressionSyntax invocation
                && IsProcessStart(invocation, model, cancellationToken)
                && invocation.ArgumentList.Arguments.Any(
                    argument => IsSymbol(argument.Expression, local, model, cancellationToken))
                && FlowsTo(invocation, processSymbol, model, cancellationToken))
            {
                return true;
            }

            if (node is AssignmentExpressionSyntax assignment
                && assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
                && IsSymbol(assignment.Right, local, model, cancellationToken)
                && AssignmentTargetBinds(assignment, processSymbol, model, cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsStartInfoCreation(
        BaseObjectCreationExpressionSyntax creation,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        return model.GetTypeInfo(creation, cancellationToken).Type is { } type
               && string.Equals(type.ToDisplayString(), StartInfoTypeName, StringComparison.Ordinal);
    }

    private static bool IsProcessStart(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        return model.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol { IsStatic: true } method
               && string.Equals(method.Name, "Start", StringComparison.Ordinal)
               && string.Equals(method.ContainingType?.ToDisplayString(), ProcessTypeName, StringComparison.Ordinal);
    }

    private static bool IsSymbol(
        ExpressionSyntax expression,
        ISymbol symbol,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        return SymbolEqualityComparer.Default.Equals(
            model.GetSymbolInfo(ProcessLaunchScope.Unwrap(expression), cancellationToken).Symbol,
            symbol);
    }

    /// <summary>
    ///     True when the process-valued expression is stored in the process symbol, by a declaration
    ///     or by an assignment.
    /// </summary>
    private static bool FlowsTo(
        ExpressionSyntax value,
        ISymbol processSymbol,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        SyntaxNode current = value;

        while (current.Parent is (ParenthesizedExpressionSyntax or PostfixUnaryExpressionSyntax) and { } next)
        {
            current = next;
        }

        return current.Parent switch
        {
            EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator } =>
                SymbolEqualityComparer.Default.Equals(
                    model.GetDeclaredSymbol(declarator, cancellationToken),
                    processSymbol),
            AssignmentExpressionSyntax assignment when ProcessLaunchScope.Unwrap(assignment.Right) == current =>
                SymbolEqualityComparer.Default.Equals(
                    model.GetSymbolInfo(assignment.Left, cancellationToken).Symbol,
                    processSymbol),
            _ => false
        };
    }

    private static bool IsStartInfoName(SimpleNameSyntax name)
    {
        return string.Equals(name.Identifier.ValueText, StartInfoMemberName, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Splits an assignment target into the property name and the receiver. An object initializer
    ///     binds a bare identifier, so its receiver is absent.
    /// </summary>
    private static bool TryGetTarget(
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
    ///     True when the process stream is read in full before the wait.
    /// </summary>
    private static bool HasCaptureReadBefore(
        SyntaxNode scope,
        ISymbol processSymbol,
        SemanticModel model,
        int waitPosition,
        CancellationToken cancellationToken)
    {
        foreach (var invocation in scope.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.SpanStart >= waitPosition
                || invocation.Expression is not MemberAccessExpressionSyntax read
                || !CaptureReadNames.Contains(read.Name.Identifier.ValueText)
                || read.Expression is not MemberAccessExpressionSyntax stream
                || !StandardStreamNames.Contains(stream.Name.Identifier.ValueText))
            {
                continue;
            }

            if (SymbolEqualityComparer.Default.Equals(
                    ProcessLaunchScope.GetRootSymbol(stream.Expression, model, cancellationToken),
                    processSymbol))
            {
                return true;
            }
        }

        return false;
    }
}

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
///     The rule reads one method body. A call that receives the process output stream as an
///     argument counts as a drain. A redirect flag binds to the process through the start info
///     member, a start info local passed to Process.Start or assigned to the process, or an
///     initializer of the same process.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ProcessOutputDrainAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128160";

    private const string ProcessTypeName = "System.Diagnostics.Process";

    private const string StartInfoTypeName = "System.Diagnostics.ProcessStartInfo";

    private const string StartInfoMemberName = "StartInfo";

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
        if (invocation.FirstAncestorOrSelf<AnonymousFunctionExpressionSyntax>() is { } lambda)
        {
            // The configuration and the wait usually sit in one method. A lambda body is the scope for a
            // wait inside a callback, because the enclosing method holds no relation to the process.
            return lambda.Body;
        }

        if (invocation.FirstAncestorOrSelf<BaseMethodDeclarationSyntax>() is { } method)
        {
            return (SyntaxNode?)method.Body ?? method.ExpressionBody;
        }

        return invocation.FirstAncestorOrSelf<BlockSyntax>();
    }

    private static bool HasRedirectedStream(
        SyntaxNode scope,
        ISymbol processSymbol,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        return scope.DescendantNodes()
            .OfType<AssignmentExpressionSyntax>()
            .Any(assignment => IsTrueRedirect(scope, assignment, processSymbol, model, cancellationToken));
    }

    private static bool IsTrueAssignment(AssignmentExpressionSyntax assignment)
    {
        return assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
               && assignment.Right is LiteralExpressionSyntax literal
               && literal.IsKind(SyntaxKind.TrueLiteralExpression);
    }

    private static bool IsStartInfoProperty(
        ExpressionSyntax left,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        return model.GetSymbolInfo(left, cancellationToken).Symbol is IPropertySymbol property
               && string.Equals(
                   property.ContainingType?.ToDisplayString(),
                   StartInfoTypeName,
                   StringComparison.Ordinal);
    }

    private static bool IsTrueRedirect(
        SyntaxNode scope,
        AssignmentExpressionSyntax assignment,
        ISymbol processSymbol,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        if (!IsTrueAssignment(assignment)
            || !TryGetRedirectTarget(assignment.Left, out var name, out var receiver)
            || !RedirectProperties.Contains(name)
            || !IsStartInfoProperty(assignment.Left, model, cancellationToken))
        {
            return false;
        }

        if (receiver is not null)
        {
            return StartInfoBinds(scope, receiver, processSymbol, model, cancellationToken);
        }

        return InitializerRedirectBinds(scope, assignment, processSymbol, model, cancellationToken);
    }

    private static bool InitializerRedirectBinds(
        SyntaxNode scope,
        AssignmentExpressionSyntax assignment,
        ISymbol processSymbol,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        // An object initializer has no receiver. The flag belongs to the info instance the
        // initializer builds, or to the start info of the process the initializer builds.
        return assignment.Parent is InitializerExpressionSyntax initializer
               && initializer.Parent switch
               {
                   BaseObjectCreationExpressionSyntax creation =>
                       StartInfoBinds(scope, creation, processSymbol, model, cancellationToken),
                   AssignmentExpressionSyntax { Parent: InitializerExpressionSyntax { Parent: BaseObjectCreationExpressionSyntax processCreation } } =>
                       FlowsTo(processCreation, processSymbol, model, cancellationToken),
                   _ => false
               };
    }

    /// <summary>
    ///     True when the start info expression belongs to the process. The expression is the
    ///     process start info member, a local that links to the process, or a new start info that
    ///     is passed to <c lang="csharp">Process.Start</c> or assigned to the process.
    /// </summary>
    private static bool StartInfoBinds(
        SyntaxNode scope,
        ExpressionSyntax expression,
        ISymbol processSymbol,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        var current = Unwrap(expression);

        if (current is BaseObjectCreationExpressionSyntax creation)
        {
            return creation.Parent is { } parent && StartInfoCreationBinds(
                scope, creation, parent, processSymbol, model, cancellationToken);
        }

        var root = GetRootSymbol(current, model, cancellationToken);

        return SymbolEqualityComparer.Default.Equals(root, processSymbol)
               || (current is IdentifierNameSyntax or MemberAccessExpressionSyntax
                   && root is not null
                   && IsLinked(scope, root, processSymbol, model, cancellationToken));
    }

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
                       && IsLinked(scope, local, processSymbol, model, cancellationToken);
            case AssignmentExpressionSyntax assignment when Unwrap(assignment.Right) == creation:
                return AssignmentBinds(scope, assignment, processSymbol, model, cancellationToken);
            case ParenthesizedExpressionSyntax or PostfixUnaryExpressionSyntax:
                return parent.Parent is { } outer && StartInfoCreationBinds(
                    scope, creation, outer, processSymbol, model, cancellationToken);
            default:
                return false;
        }
    }

    /// <summary>
    ///     True for an assignment of a start info to the process start info member, to a process
    ///     initializer member, or to a local that links to the process.
    /// </summary>
    private static bool AssignmentBinds(
        SyntaxNode scope,
        AssignmentExpressionSyntax assignment,
        ISymbol processSymbol,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        if (assignment is
            {
                Parent: InitializerExpressionSyntax { Parent: BaseObjectCreationExpressionSyntax owner },
                Left: IdentifierNameSyntax startInfoName
            }
            && IsStartInfoName(startInfoName))
        {
            return FlowsTo(owner, processSymbol, model, cancellationToken);
        }

        if (assignment.Left is MemberAccessExpressionSyntax access && IsStartInfoName(access.Name))
        {
            return SymbolEqualityComparer.Default.Equals(
                GetRootSymbol(access.Expression, model, cancellationToken),
                processSymbol);
        }

        return model.GetSymbolInfo(assignment.Left, cancellationToken).Symbol is { } target
               && IsLinked(scope, target, processSymbol, model, cancellationToken);
    }

    /// <summary>
    ///     True when the start info symbol reaches the process. The scan looks for
    ///     <c lang="csharp">process.StartInfo = info</c>, <c lang="csharp">Process.Start(info)</c>
    ///     whose result is the process, and a process initializer that sets the start info.
    /// </summary>
    private static bool IsLinked(
        SyntaxNode scope,
        ISymbol infoSymbol,
        ISymbol processSymbol,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        foreach (var node in scope.DescendantNodes())
        {
            if (node is AssignmentExpressionSyntax assignment
                && assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
                && IsSymbol(assignment.Right, infoSymbol, model, cancellationToken)
                && AssignsStartInfo(assignment, processSymbol, model, cancellationToken))
            {
                return true;
            }

            if (node is InvocationExpressionSyntax invocation
                && IsProcessStart(invocation, model, cancellationToken)
                && invocation.ArgumentList.Arguments.Any(
                    argument => IsSymbol(argument.Expression, infoSymbol, model, cancellationToken))
                && FlowsTo(invocation, processSymbol, model, cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    private static bool AssignsStartInfo(
        AssignmentExpressionSyntax assignment,
        ISymbol processSymbol,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        return assignment.Left is MemberAccessExpressionSyntax access && IsStartInfoName(access.Name)
            ? SymbolEqualityComparer.Default.Equals(
                GetRootSymbol(access.Expression, model, cancellationToken),
                processSymbol)
            : assignment is { Left: IdentifierNameSyntax startInfoName, Parent: InitializerExpressionSyntax { Parent: BaseObjectCreationExpressionSyntax owner } }
              && IsStartInfoName(startInfoName)
              && FlowsTo(owner, processSymbol, model, cancellationToken);
    }

    private static bool IsStartInfoName(SimpleNameSyntax name)
    {
        return string.Equals(name.Identifier.ValueText, StartInfoMemberName, StringComparison.Ordinal);
    }

    private static bool IsSymbol(
        ExpressionSyntax expression,
        ISymbol symbol,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        return SymbolEqualityComparer.Default.Equals(
            model.GetSymbolInfo(Unwrap(expression), cancellationToken).Symbol,
            symbol);
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
            AssignmentExpressionSyntax assignment when Unwrap(assignment.Right) == current =>
                SymbolEqualityComparer.Default.Equals(
                    model.GetSymbolInfo(assignment.Left, cancellationToken).Symbol,
                    processSymbol),
            _ => false
        };
    }

    private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
    {
        var current = expression;

        while (true)
        {
            switch (current)
            {
                case ParenthesizedExpressionSyntax parenthesized:
                    current = parenthesized.Expression;
                    break;

                case PostfixUnaryExpressionSyntax postfix when postfix.IsKind(SyntaxKind.SuppressNullableWarningExpression):
                    current = postfix.Operand;
                    break;

                default:
                    return current;
            }
        }
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
    ///     True for a read method on the process stream, or for a call that receives the process
    ///     output stream as an argument. A helper that gets the stream is assumed to drain it.
    /// </summary>
    private static bool IsStandardStreamName(string name)
    {
        return string.Equals(name, "StandardOutput", StringComparison.Ordinal)
               || string.Equals(name, "StandardError", StringComparison.Ordinal);
    }

    private static bool IsDrainCall(
        InvocationExpressionSyntax invocation,
        ISymbol processSymbol,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        return (invocation.Expression is MemberAccessExpressionSyntax access
                && DrainMethodNames.Contains(access.Name.Identifier.ValueText)
                && SymbolEqualityComparer.Default.Equals(
                    GetRootSymbol(access.Expression, model, cancellationToken),
                    processSymbol))
               || invocation.ArgumentList.Arguments.Any(argument => argument.Expression
            .DescendantNodesAndSelf()
            .OfType<MemberAccessExpressionSyntax>()
            .Any(member => IsStandardStreamName(member.Name.Identifier.ValueText)
                           && SymbolEqualityComparer.Default.Equals(
                               GetRootSymbol(member.Expression, model, cancellationToken),
                               processSymbol)));
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
                InvocationExpressionSyntax invocation =>
                    IsDrainCall(invocation, processSymbol, model, cancellationToken),
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

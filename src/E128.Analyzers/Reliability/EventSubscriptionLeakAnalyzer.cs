using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

using Removal = (Microsoft.CodeAnalysis.IEventSymbol Event, Microsoft.CodeAnalysis.ISymbol? Receiver);
using Subscription = (Microsoft.CodeAnalysis.IEventSymbol Event, Microsoft.CodeAnalysis.ISymbol? Receiver, Microsoft.CodeAnalysis.Location Location);

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128176: Reports an event subscription whose handler holds the subscriber, where no
///     <see langword="Dispose" /> method of the subscriber type removes the same subscription.
///     The publisher holds a reference to the subscriber, so the subscriber outlives its use.
/// </summary>
/// <remarks>
///     The rule reads a disposable type only. A type that never disposes has no cleanup site to
///     compare against, and the remedy is a new lifetime, not a missing line. A removal in any
///     method of the subscriber type counts, because the dispose path can call a helper that owns
///     the cleanup. Handler identity is not part of the check. The removal must name the same event
///     on the same publisher receiver, because another publisher instance holds its own handler
///     list. The analysis runs per named type, so a partial type that removes the subscription in
///     another file is covered.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EventSubscriptionLeakAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128176";

    private const string DisposableTypeName = "System.IDisposable";

    private const string AsyncDisposableTypeName = "System.IAsyncDisposable";

    private const string DisposeName = "Dispose";

    private const string DisposeAsyncName = "DisposeAsync";

    private const string DisposeAsyncCoreName = "DisposeAsyncCore";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Event subscription is never removed",
        "The subscription to '{0}' is never removed, and the handler captures this, so the publisher keeps the subscriber alive",
        "E128-Reliability",
        DiagnosticSeverity.Warning,
        true,
        "An event handler that captures this keeps the subscriber reachable from the publisher. " +
        "Remove the subscription with -= in Dispose, or hold the delegate in a field so the " +
        "subscription can be removed later.");

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
        if (context.Compilation.GetTypeByMetadataName(DisposableTypeName) is null)
        {
            return;
        }

        context.RegisterSymbolStartAction(OnSymbolStart, SymbolKind.NamedType);
    }

    private static void OnSymbolStart(SymbolStartAnalysisContext context)
    {
        if (context.Symbol is not INamedTypeSymbol subscriber || !IsDisposable(subscriber))
        {
            return;
        }

        var subscriptions = new ConcurrentQueue<Subscription>();
        var removals = new ConcurrentDictionary<Removal, bool>(RemovalComparer.Instance);
        var disposedFields = new ConcurrentDictionary<IFieldSymbol, bool>(SymbolEqualityComparer.Default);

        context.RegisterSyntaxNodeAction(
            ctx => CollectSubscription(ctx, subscriber, subscriptions),
            SyntaxKind.AddAssignmentExpression);

        context.RegisterSyntaxNodeAction(
            ctx => CollectRemoval(ctx, subscriber, removals),
            SyntaxKind.SubtractAssignmentExpression);

        context.RegisterSyntaxNodeAction(
            ctx => CollectDisposedField(ctx, disposedFields),
            SyntaxKind.InvocationExpression);

        context.RegisterSymbolEndAction(ctx => Report(subscriptions, removals, disposedFields, ctx));
    }

    private static void CollectSubscription(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol subscriber,
        ConcurrentQueue<Subscription> subscriptions)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;
        var model = context.SemanticModel;
        var token = context.CancellationToken;

        if (model.GetSymbolInfo(assignment.Left, token).Symbol is not IEventSymbol eventSymbol)
        {
            return;
        }

        // A subscription to an event on this instance dies with the instance, so nothing leaks.
        if (!eventSymbol.IsStatic && IsOwnInstanceEvent(assignment.Left))
        {
            return;
        }

        if (!CapturesSubscriber(assignment.Right, model, subscriber, token))
        {
            return;
        }

        subscriptions.Enqueue(
            (eventSymbol, GetReceiver(assignment.Left, model, token), assignment.GetLocation()));
    }

    private static bool IsOwnInstanceEvent(ExpressionSyntax left)
    {
        return left is IdentifierNameSyntax
            or MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax };
    }

    // The storage that holds the publisher in "publisher.Event += handler", or null for a member
    // name that binds to this instance.
    private static ISymbol? GetReceiver(ExpressionSyntax left, SemanticModel model, CancellationToken token)
    {
        return left is MemberAccessExpressionSyntax access
            ? model.GetSymbolInfo(access.Expression, token).Symbol
            : null;
    }

    private static void CollectDisposedField(
        SyntaxNodeAnalysisContext context,
        ConcurrentDictionary<IFieldSymbol, bool> disposedFields)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (!IsInDisposeMethod(invocation)
            || DisposeCalls.GetReceiver(invocation) is not { } receiver
            || context.SemanticModel.GetSymbolInfo(receiver, context.CancellationToken).Symbol is not IFieldSymbol field)
        {
            return;
        }

        disposedFields[field] = true;
    }

    private static void CollectRemoval(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol subscriber,
        ConcurrentDictionary<Removal, bool> removals)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;
        var model = context.SemanticModel;
        var token = context.CancellationToken;

        // A removal in another type, such as a nested type, does not clean up this subscriber.
        if (!IsInSubscriber(assignment, subscriber, model, token))
        {
            return;
        }

        if (model.GetSymbolInfo(assignment.Left, token).Symbol is not IEventSymbol eventSymbol)
        {
            return;
        }

        removals[(eventSymbol, GetReceiver(assignment.Left, model, token))] = true;
    }

    private static bool IsInSubscriber(
        SyntaxNode node,
        INamedTypeSymbol subscriber,
        SemanticModel model,
        CancellationToken token)
    {
        return node.FirstAncestorOrSelf<TypeDeclarationSyntax>() is { } declaration
               && SymbolEqualityComparer.Default.Equals(
                   model.GetDeclaredSymbol(declaration, token),
                   subscriber);
    }

    private static void Report(
        ConcurrentQueue<Subscription> subscriptions,
        ConcurrentDictionary<Removal, bool> removals,
        ConcurrentDictionary<IFieldSymbol, bool> disposedFields,
        SymbolAnalysisContext context)
    {
        foreach (var (eventSymbol, receiver, location) in subscriptions)
        {
            // A publisher field that the subscriber owns and disposes dies with the subscriber.
            var ownedAndDisposed = receiver is IFieldSymbol { IsStatic: false } publisher
                                   && disposedFields.ContainsKey(publisher);

            if (ownedAndDisposed || IsRemoved(removals, eventSymbol, receiver))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(Rule, location, eventSymbol.Name));
        }
    }

    // A static event has one handler list for every caller, so any removal of it counts. An instance
    // event sits on one publisher object, so the removal must name that same receiver.
    private static bool IsRemoved(
        ConcurrentDictionary<Removal, bool> removals,
        IEventSymbol eventSymbol,
        ISymbol? receiver)
    {
        foreach (var (removedEvent, removedReceiver) in removals.Keys)
        {
            if (!SymbolEqualityComparer.Default.Equals(removedEvent, eventSymbol))
            {
                continue;
            }

            if (eventSymbol.IsStatic || SymbolEqualityComparer.Default.Equals(removedReceiver, receiver))
            {
                return true;
            }
        }

        return false;
    }

    private sealed class RemovalComparer : IEqualityComparer<Removal>
    {
        internal static readonly RemovalComparer Instance = new();

        public bool Equals(Removal x, Removal y)
        {
            return SymbolEqualityComparer.Default.Equals(x.Event, y.Event)
                   && SymbolEqualityComparer.Default.Equals(x.Receiver, y.Receiver);
        }

        public int GetHashCode(Removal obj)
        {
            var hash = SymbolEqualityComparer.Default.GetHashCode(obj.Event);

            return obj.Receiver is null
                ? hash
                : (hash * 397) ^ SymbolEqualityComparer.Default.GetHashCode(obj.Receiver);
        }
    }

    private static bool IsDisposable(INamedTypeSymbol type)
    {
        return type.AllInterfaces.Any(
            static candidate => string.Equals(candidate.ToDisplayString(), DisposableTypeName, StringComparison.Ordinal)
                         || string.Equals(candidate.ToDisplayString(), AsyncDisposableTypeName, StringComparison.Ordinal));
    }

    /// <summary>
    ///     True when the handler reads this instance, directly or through an unqualified member
    ///     name. A handler that reads instance state of the subscriber keeps it alive.
    /// </summary>
    private static bool CapturesSubscriber(
        ExpressionSyntax handler,
        SemanticModel model,
        INamedTypeSymbol subscriber,
        CancellationToken cancellationToken)
    {
        return handler is AnonymousFunctionExpressionSyntax anonymous
            ? BodyReadsInstance(anonymous, model, subscriber, cancellationToken)
            : model.GetSymbolInfo(handler, cancellationToken).Symbol is { IsStatic: false } member
              && SymbolEqualityComparer.Default.Equals(member.ContainingType, subscriber);
    }

    private static bool BodyReadsInstance(
        AnonymousFunctionExpressionSyntax anonymous,
        SemanticModel model,
        INamedTypeSymbol subscriber,
        CancellationToken cancellationToken)
    {
        foreach (var node in anonymous.DescendantNodes())
        {
            if (node is AnonymousFunctionExpressionSyntax)
            {
                continue;
            }

            if (node is ThisExpressionSyntax)
            {
                return true;
            }

            if (node is not IdentifierNameSyntax identifier)
            {
                continue;
            }

            if (identifier.Parent is MemberAccessExpressionSyntax access && access.Name == identifier)
            {
                // A member on some other receiver, so the read does not reach this instance.
                continue;
            }

            if (ReadsInstanceMember(identifier, model, subscriber, cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ReadsInstanceMember(
        IdentifierNameSyntax identifier,
        SemanticModel model,
        INamedTypeSymbol subscriber,
        CancellationToken cancellationToken)
    {
        return model.GetSymbolInfo(identifier, cancellationToken).Symbol is { IsStatic: false } member
               && member is IFieldSymbol or IPropertySymbol or IMethodSymbol or IEventSymbol
               && SymbolEqualityComparer.Default.Equals(member.ContainingType, subscriber);
    }

    private static bool IsInDisposeMethod(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (current is MethodDeclarationSyntax method)
            {
                return string.Equals(method.Identifier.ValueText, DisposeName, StringComparison.Ordinal)
                       || string.Equals(method.Identifier.ValueText, DisposeAsyncName, StringComparison.Ordinal)
                       || string.Equals(method.Identifier.ValueText, DisposeAsyncCoreName, StringComparison.Ordinal);
            }

            if (current is AccessorDeclarationSyntax or LocalFunctionStatementSyntax or BaseTypeDeclarationSyntax)
            {
                return false;
            }
        }

        return false;
    }
}

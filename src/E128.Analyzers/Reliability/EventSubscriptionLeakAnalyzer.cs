using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

using Subscription = (Microsoft.CodeAnalysis.IEventSymbol Event, Microsoft.CodeAnalysis.IFieldSymbol? Publisher, Microsoft.CodeAnalysis.Location Location);

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128176: Reports an event subscription whose handler holds the subscriber, where no
///     <see langword="Dispose" /> method of the subscriber type removes the same subscription.
///     The publisher holds a reference to the subscriber, so the subscriber outlives its use.
/// </summary>
/// <remarks>
///     The rule reads a disposable type only. A type that never disposes has no cleanup site to
///     compare against, and the remedy is a new lifetime, not a missing line. Any removal in a
///     dispose method counts, because handler identity is not part of the check. The analysis runs
///     per named type, so a partial type that removes the subscription in another file is covered.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EventSubscriptionLeakAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128176";

    internal const string DisposableTypeName = "System.IDisposable";

    internal const string AsyncDisposableTypeName = "System.IAsyncDisposable";

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
        var removals = new ConcurrentDictionary<IEventSymbol, bool>(SymbolEqualityComparer.Default);
        var disposedFields = new ConcurrentDictionary<IFieldSymbol, bool>(SymbolEqualityComparer.Default);

        context.RegisterSyntaxNodeAction(
            ctx => CollectSubscription(ctx, subscriber, subscriptions),
            SyntaxKind.AddAssignmentExpression);

        context.RegisterSyntaxNodeAction(
            ctx => CollectRemoval(ctx, removals),
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

        if (CapturesSubscriber(assignment.Right, model, subscriber, token))
        {
            subscriptions.Enqueue((eventSymbol, GetPublisherField(assignment.Left, model, token), assignment.GetLocation()));
        }
    }

    private static bool IsOwnInstanceEvent(ExpressionSyntax left)
    {
        return left is IdentifierNameSyntax
            or MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax };
    }

    // The field that holds the publisher in "field.Event += handler", or null for any other receiver.
    private static IFieldSymbol? GetPublisherField(ExpressionSyntax left, SemanticModel model, CancellationToken token)
    {
        return left is MemberAccessExpressionSyntax access
               && model.GetSymbolInfo(access.Expression, token).Symbol is IFieldSymbol { IsStatic: false } field
            ? field
            : null;
    }

    private static void CollectDisposedField(
        SyntaxNodeAnalysisContext context,
        ConcurrentDictionary<IFieldSymbol, bool> disposedFields)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (IsInDisposeMethod(invocation)
            && DisposeCalls.GetReceiver(invocation) is { } receiver
            && context.SemanticModel.GetSymbolInfo(receiver, context.CancellationToken).Symbol is IFieldSymbol field)
        {
            disposedFields[field] = true;
        }
    }

    private static void CollectRemoval(
        SyntaxNodeAnalysisContext context,
        ConcurrentDictionary<IEventSymbol, bool> removals)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;

        if (!IsInDisposeMethod(assignment))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(assignment.Left, context.CancellationToken).Symbol
            is IEventSymbol eventSymbol)
        {
            removals[eventSymbol] = true;
        }
    }

    private static void Report(
        ConcurrentQueue<Subscription> subscriptions,
        ConcurrentDictionary<IEventSymbol, bool> removals,
        ConcurrentDictionary<IFieldSymbol, bool> disposedFields,
        SymbolAnalysisContext context)
    {
        foreach (var (eventSymbol, publisher, location) in subscriptions)
        {
            // A publisher field that the subscriber owns and disposes dies with the subscriber.
            var ownedAndDisposed = publisher is not null && disposedFields.ContainsKey(publisher);

            if (!ownedAndDisposed && !removals.ContainsKey(eventSymbol))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, location, eventSymbol.Name));
            }
        }
    }

    private static bool IsDisposable(INamedTypeSymbol type)
    {
        return type.AllInterfaces.Any(
            candidate => string.Equals(candidate.ToDisplayString(), DisposableTypeName, StringComparison.Ordinal)
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

            if (model.GetSymbolInfo(identifier, cancellationToken).Symbol is { IsStatic: false } member
                && member is IFieldSymbol or IPropertySymbol or IMethodSymbol or IEventSymbol
                && SymbolEqualityComparer.Default.Equals(member.ContainingType, subscriber))
            {
                return true;
            }
        }

        return false;
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

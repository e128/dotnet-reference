using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128182: Reports a second enumeration of the same <c lang="csharp">IAsyncEnumerable&lt;T&gt;</c>
///     value inside one member body. The first enumeration moves the sequence toward its end, so
///     the second enumeration sees a different sequence or an empty one.
/// </summary>
/// <remarks>
///     The rule reads one member body. A value created inline, such as a call, stays out, because
///     each call may return a fresh sequence. A lambda, local function, or accessor body runs as
///     its own unit, so an enumeration inside it does not pair with one outside it. An iterator
///     declared with <c lang="csharp">yield return</c> stays out, because it hands items onward one
///     at a time.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AsyncEnumerableConsumedTwiceAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128182";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "IAsyncEnumerable is consumed twice",
        "The async sequence '{0}' is already consumed on an earlier line, so a second enumeration can re-run the source or see an empty sequence",
        "E128-Reliability",
        DiagnosticSeverity.Warning,
        true,
        "A second enumeration of an IAsyncEnumerable<T> value can re-run the source, and a single-pass "
        + "source such as a channel reader yields nothing the second time. Materialize the sequence "
        + "once, or call the source factory again.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(compilationContext =>
        {
            var asyncEnumerableType = compilationContext.Compilation.GetTypeByMetadataName(
                "System.Collections.Generic.IAsyncEnumerable`1");

            // A target framework without IAsyncEnumerable cannot hold the value this rule reads.
            compilationContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeBody(nodeContext, asyncEnumerableType),
                SyntaxKind.MethodDeclaration,
                SyntaxKind.LocalFunctionStatement,
                SyntaxKind.SimpleLambdaExpression,
                SyntaxKind.ParenthesizedLambdaExpression,
                SyntaxKind.AnonymousMethodExpression,
                SyntaxKind.GetAccessorDeclaration,
                SyntaxKind.SetAccessorDeclaration,
                SyntaxKind.InitAccessorDeclaration,
                SyntaxKind.AddAccessorDeclaration,
                SyntaxKind.RemoveAccessorDeclaration);
        });
    }

    private static SyntaxNode? GetFunctionBody(SyntaxNode node)
    {
        return node switch
        {
            MethodDeclarationSyntax method => (SyntaxNode?)method.Body ?? method.ExpressionBody?.Expression,
            LocalFunctionStatementSyntax localFunction => (SyntaxNode?)localFunction.Body
                                                          ?? localFunction.ExpressionBody?.Expression,
            AccessorDeclarationSyntax accessor => (SyntaxNode?)accessor.Body
                                                  ?? accessor.ExpressionBody?.Expression,
            AnonymousFunctionExpressionSyntax anonymous => (SyntaxNode?)anonymous.Body,
            _ => null
        };
    }

    private static void AnalyzeBody(SyntaxNodeAnalysisContext context, INamedTypeSymbol? asyncEnumerableType)
    {
        if (asyncEnumerableType is null)
        {
            return;
        }

        var body = GetFunctionBody(context.Node);

        if (body is null)
        {
            return;
        }

        // An iterator hands items onward one at a time, so it reads the source once per call.
        if (body.DescendantNodesAndSelf(IsFunctionBoundary).Any(static node => node is YieldStatementSyntax))
        {
            return;
        }

        var model = context.SemanticModel;
        var cancellationToken = context.CancellationToken;
        var firstSites = new Dictionary<ISymbol, ExpressionSyntax>(SymbolEqualityComparer.Default);

        foreach (var node in body.DescendantNodesAndSelf(IsFunctionBoundary))
        {
            // An assignment gives the symbol a new sequence, so earlier enumerations no longer count.
            if (node is AssignmentExpressionSyntax assignment
                && model.GetSymbolInfo(assignment.Left, cancellationToken).Symbol is { } assigned)
            {
                firstSites.Remove(assigned);
            }

            if (!TryGetEnumeratedSequence(node, model, asyncEnumerableType, cancellationToken, out var expression, out var symbol))
            {
                continue;
            }

            if (!firstSites.TryGetValue(symbol, out var first))
            {
                firstSites.Add(symbol, expression);
                continue;
            }

            // Two enumerations in the two branches of one if never run in the same pass.
            if (ExclusiveBranches.Are(first, expression))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                Rule,
                expression.GetLocation(),
                ImmutableArray.Create(first.GetLocation()),
                symbol.Name));
        }
    }

    private static bool TryGetEnumeratedSequence(
        SyntaxNode node,
        SemanticModel model,
        INamedTypeSymbol asyncEnumerableType,
        CancellationToken cancellationToken,
        [NotNullWhen(true)] out ExpressionSyntax? expression,
        [NotNullWhen(true)] out ISymbol? symbol)
    {
        expression = null;
        symbol = null;

        if (!TryGetEnumerationSource(node, out var source))
        {
            return false;
        }

        var unwrapped = Unwrap(source);

        if (!TryGetTrackedSymbol(model, unwrapped, cancellationToken, out var tracked)
            || !IsAsyncEnumerable(tracked, asyncEnumerableType))
        {
            return false;
        }

        expression = unwrapped;
        symbol = tracked;

        return true;
    }

    private static bool IsFunctionBoundary(SyntaxNode node)
    {
        return node is not (AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax);
    }

    private static bool TryGetEnumerationSource(SyntaxNode node, [NotNullWhen(true)] out ExpressionSyntax? source)
    {
        switch (node)
        {
            case CommonForEachStatementSyntax forEach when forEach.AwaitKeyword != default:
                source = forEach.Expression;
                return true;

            case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access }
                when string.Equals(access.Name.Identifier.ValueText, "GetAsyncEnumerator", StringComparison.Ordinal):
                source = access.Expression;
                return true;

            default:
                source = null;
                return false;
        }
    }

    private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
    {
        while (true)
        {
            switch (expression)
            {
                case ParenthesizedExpressionSyntax parenthesized:
                    expression = parenthesized.Expression;
                    continue;

                case InvocationExpressionSyntax
                {
                    Expression: MemberAccessExpressionSyntax access,
                    ArgumentList.Arguments.Count: 1
                } when IsWrapper(access.Name.Identifier.ValueText):
                    expression = access.Expression;
                    continue;

                default:
                    return expression;
            }
        }
    }

    private static bool IsWrapper(string name)
    {
        return string.Equals(name, "WithCancellation", StringComparison.Ordinal)
               || string.Equals(name, "ConfigureAwait", StringComparison.Ordinal);
    }

    private static bool TryGetTrackedSymbol(
        SemanticModel model,
        ExpressionSyntax expression,
        CancellationToken cancellationToken,
        [NotNullWhen(true)] out ISymbol? symbol)
    {
        symbol = null;

        if (expression is MemberAccessExpressionSyntax access)
        {
            // A field on another instance is a different sequence, so only this and base qualify.
            if (access.Expression is not (ThisExpressionSyntax or BaseExpressionSyntax))
            {
                return false;
            }
        }
        else if (expression is not IdentifierNameSyntax)
        {
            return false;
        }

        symbol = model.GetSymbolInfo(expression, cancellationToken).Symbol;

        return symbol is ILocalSymbol or IParameterSymbol or IFieldSymbol;
    }

    private static bool IsAsyncEnumerable(ISymbol symbol, INamedTypeSymbol asyncEnumerableType)
    {
        var type = symbol switch
        {
            ILocalSymbol local => local.Type,
            IParameterSymbol parameter => parameter.Type,
            IFieldSymbol field => field.Type,
            _ => null
        };

        return type is not null
               && (IsConstructedFrom(type, asyncEnumerableType)
                   || type.AllInterfaces.Any(implemented => IsConstructedFrom(implemented, asyncEnumerableType)));
    }

    private static bool IsConstructedFrom(ITypeSymbol type, INamedTypeSymbol definition)
    {
        return type is INamedTypeSymbol named
               && SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, definition);
    }
}

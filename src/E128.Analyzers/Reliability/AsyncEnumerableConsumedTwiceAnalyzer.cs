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
///     each call may return a fresh sequence. A lambda or local function body stays out of its
///     enclosing body and runs as its own unit. An iterator declared with
///     <c lang="csharp">yield return</c> stays out, because it hands items onward one at a time.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AsyncEnumerableConsumedTwiceAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128182";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "IAsyncEnumerable is consumed twice",
        "The async sequence '{0}' is already consumed on an earlier line, so the second enumeration sees a different or empty sequence",
        "E128-Reliability",
        DiagnosticSeverity.Warning,
        true,
        "An IAsyncEnumerable<T> value holds one enumerator position. A second enumeration of the "
        + "same value starts at the end or replays a different sequence, so the loop body sees "
        + "different or empty data. Materialize the sequence once, or call the source factory again.");

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
            if (asyncEnumerableType is null)
            {
                return;
            }

            compilationContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeBody(nodeContext, asyncEnumerableType),
                SyntaxKind.MethodDeclaration,
                SyntaxKind.LocalFunctionStatement);
        });
    }

    private static void AnalyzeBody(SyntaxNodeAnalysisContext context, INamedTypeSymbol asyncEnumerableType)
    {
        var body = context.Node switch
        {
            MethodDeclarationSyntax method => (SyntaxNode?)method.Body ?? method.ExpressionBody?.Expression,
            LocalFunctionStatementSyntax localFunction => (SyntaxNode?)localFunction.Body
                                                          ?? localFunction.ExpressionBody?.Expression,
            _ => null
        };

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
        var firstSites = new Dictionary<ISymbol, Location>(SymbolEqualityComparer.Default);

        foreach (var node in body.DescendantNodesAndSelf(IsFunctionBoundary))
        {
            if (!TryGetEnumerationSource(node, out var source))
            {
                continue;
            }

            var expression = Unwrap(source);
            if (!TryGetTrackedSymbol(model, expression, cancellationToken, out var symbol)
                || IsEnumeratorCancellationParameter(symbol)
                || !IsAsyncEnumerable(symbol, asyncEnumerableType))
            {
                continue;
            }

            var location = expression.GetLocation();
            if (!firstSites.TryGetValue(symbol, out var firstLocation))
            {
                firstSites.Add(symbol, location);
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                Rule,
                location,
                ImmutableArray.Create(firstLocation),
                symbol.Name));
        }
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

    private static bool IsEnumeratorCancellationParameter(ISymbol symbol)
    {
        return symbol is IParameterSymbol parameter
               && parameter.GetAttributes().Any(
                   static attribute => attribute.AttributeClass is INamedTypeSymbol attributeClass
                                       && string.Equals(
                                           attributeClass.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                                           "global::System.Runtime.CompilerServices.EnumeratorCancellationAttribute",
                                           StringComparison.Ordinal));
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

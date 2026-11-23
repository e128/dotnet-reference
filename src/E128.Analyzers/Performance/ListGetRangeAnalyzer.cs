using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ListGetRangeAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128084";

    private const int GetRangeArgumentCount = 2;

    private const string ListMetadataName = "System.Collections.Generic.List`1";
    private const string CollectionsMarshalMetadataName = "System.Runtime.InteropServices.CollectionsMarshal";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Use CollectionsMarshal.AsSpan with Slice instead of List.GetRange",
        "List<T>.GetRange allocates an intermediate list; use CollectionsMarshal.AsSpan(list).Slice(start, count).ToArray()",
        "E128-Performance",
        DiagnosticSeverity.Warning,
        true,
        "List<T>.GetRange(start, count) allocates an intermediate List<T>. " +
        "CollectionsMarshal.AsSpan(list).Slice(start, count).ToArray() copies directly from the internal array. " +
        "The rule skips a call whose result stays a List<T>. The fixed code throws ArgumentOutOfRangeException, not ArgumentException, on a bad range.");

    private static readonly DiagnosticDescriptor LoopRule = new(
        DiagnosticId,
        "Use CollectionsMarshal.AsSpan instead of a List indexer loop",
        "The for loop indexes a List<T> and repeats the bounds check per element. Enumerate CollectionsMarshal.AsSpan(list) instead.",
        "E128-Performance",
        DiagnosticSeverity.Warning,
        true,
        "CollectionsMarshal.AsSpan returns a span over the internal array with no copy. " +
        "The rule stays silent when the loop mutates the list or can run other code that may resize it, because the span reads stale memory after a resize.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule, LoopRule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static compilationContext =>
        {
            var marshalType = compilationContext.Compilation.GetTypeByMetadataName(CollectionsMarshalMetadataName);
            if (marshalType is null)
            {
                return;
            }

            var listType = compilationContext.Compilation.GetTypeByMetadataName(ListMetadataName);
            if (listType is null)
            {
                return;
            }

            compilationContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeInvocation(nodeContext, listType),
                SyntaxKind.InvocationExpression);

            compilationContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeLoop(nodeContext, listType),
                SyntaxKind.ForStatement);
        });
    }

    private static void AnalyzeLoop(SyntaxNodeAnalysisContext context, INamedTypeSymbol listType)
    {
        if (!TryMatchIndexerLoop(
                (ForStatementSyntax)context.Node,
                context.SemanticModel,
                listType,
                context.CancellationToken,
                out _,
                out _))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(LoopRule, context.Node.GetLocation()));
    }

    /// <summary>
    ///     Matches a <c lang="csharp">for (int i = 0; i &lt; list.Count; i++)</c> loop whose body
    ///     only reads <c lang="csharp">list[i]</c>. The analyzer and the fix provider call this one
    ///     helper.
    /// </summary>
    internal static bool TryMatchIndexerLoop(
        ForStatementSyntax loop,
        SemanticModel model,
        INamedTypeSymbol listType,
        CancellationToken cancellationToken,
        out ExpressionSyntax? list,
        out ImmutableArray<ElementAccessExpressionSyntax> reads)
    {
        list = null;
        reads = [];

        if (!TryMatchHeader(loop, model, listType, cancellationToken, out var owner, out var declaration)
            || owner is null
            || declaration is null)
        {
            return false;
        }

        var listSymbol = model.GetSymbolInfo(owner, cancellationToken).Symbol;
        var loopVariable = model.GetDeclaredSymbol(declaration.Variables[0], cancellationToken);

        if (listSymbol is not (ILocalSymbol or IParameterSymbol or IFieldSymbol) || loopVariable is null)
        {
            return false;
        }

        if (!TryCollectReads(loop, model, loopVariable, listSymbol, out var builder, cancellationToken))
        {
            return false;
        }

        if (builder.Count is 0)
        {
            return false;
        }

        list = owner;
        reads = builder.ToImmutable();
        return true;
    }

    private static bool TryMatchHeader(
        ForStatementSyntax loop,
        SemanticModel model,
        INamedTypeSymbol listType,
        CancellationToken cancellationToken,
        out ExpressionSyntax? owner,
        out VariableDeclarationSyntax? declaration)
    {
        owner = null;
        declaration = null;

        if (loop.Declaration is not { Variables.Count: 1 } variables)
        {
            return false;
        }

        if (!IndexedForLoop.TryMatch(loop, "Count", out _, out var boundOwner, out _) || boundOwner is null)
        {
            return false;
        }

        if (!IsNameChain(boundOwner))
        {
            return false;
        }

        if (!TypeSymbols.MatchesOriginalDefinition(
                model.GetTypeInfo(boundOwner, cancellationToken).Type,
                listType))
        {
            return false;
        }

        owner = boundOwner;
        declaration = variables;
        return true;
    }

    private static bool TryCollectReads(
        ForStatementSyntax loop,
        SemanticModel model,
        ISymbol loopVariable,
        ISymbol listSymbol,
        out ImmutableArray<ElementAccessExpressionSyntax>.Builder builder,
        CancellationToken cancellationToken)
    {
        builder = ImmutableArray.CreateBuilder<ElementAccessExpressionSyntax>();

        foreach (var identifier in loop.Statement.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
        {
            var symbol = model.GetSymbolInfo(identifier, cancellationToken).Symbol;

            if (SymbolEqualityComparer.Default.Equals(symbol, loopVariable))
            {
                if (!TryAddElementRead(identifier, model, listSymbol, builder, cancellationToken))
                {
                    return false;
                }

                continue;
            }

            if (SymbolEqualityComparer.Default.Equals(symbol, listSymbol) && !IsElementReceiver(identifier))
            {
                return false;
            }
        }

        return !BodyMayRunOtherCode(loop.Statement, model, cancellationToken);
    }

    /// <summary>
    ///     Reports whether the loop body can hand control to code that may resize the list through
    ///     another reference: an await, a yield, a call into source code or an overridable member,
    ///     a delegate call, or a source property accessor with a body. A span over the list keeps
    ///     the old buffer after such a resize. A user-defined operator or conversion that resizes
    ///     the list is outside this check.
    /// </summary>
    private static bool BodyMayRunOtherCode(
        StatementSyntax body,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        foreach (var node in body.DescendantNodesAndSelf())
        {
            if (NodeMayRunOtherCode(node, model, cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    private static bool NodeMayRunOtherCode(SyntaxNode node, SemanticModel model, CancellationToken cancellationToken)
    {
        return node switch
        {
            AwaitExpressionSyntax or YieldStatementSyntax => true,
            InvocationExpressionSyntax or BaseObjectCreationExpressionSyntax or IdentifierNameSyntax
                or ElementAccessExpressionSyntax =>
                RunsOtherCode(model.GetSymbolInfo(node, cancellationToken).Symbol, cancellationToken),
            _ => false
        };
    }

    private static bool RunsOtherCode(ISymbol? symbol, CancellationToken cancellationToken)
    {
        return symbol switch
        {
            IMethodSymbol method => method.MethodKind is MethodKind.DelegateInvoke
                                    || HasSourceBody(method, cancellationToken)
                                    || IsOverridable(method),
            IPropertySymbol property => HasSourceBody(property.GetMethod, cancellationToken)
                                        || HasSourceBody(property.SetMethod, cancellationToken)
                                        || IsOverridable(property.GetMethod)
                                        || IsOverridable(property.SetMethod),
            _ => false
        };
    }

    private static bool HasSourceBody(IMethodSymbol? method, CancellationToken cancellationToken)
    {
        if (method is null)
        {
            return false;
        }

        foreach (var reference in method.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax(cancellationToken) is not AccessorDeclarationSyntax { Body: null, ExpressionBody: null })
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsOverridable(IMethodSymbol? method)
    {
        return method is not null
               && !method.IsSealed
               && !method.ContainingType.IsSealed
               && (method.IsVirtual
                   || method.IsAbstract
                   || method.IsOverride
                   || method.ContainingType.TypeKind is TypeKind.Interface);
    }

    private static bool TryAddElementRead(
        IdentifierNameSyntax identifier,
        SemanticModel model,
        ISymbol listSymbol,
        ImmutableArray<ElementAccessExpressionSyntax>.Builder builder,
        CancellationToken cancellationToken)
    {
        if (identifier.Parent is not ArgumentSyntax argument
            || argument.Parent?.Parent is not ElementAccessExpressionSyntax element
            || element.ArgumentList.Arguments.Count is not 1
            || element.ArgumentList.Arguments[0].Span != argument.Span
            || IsWrite(element))
        {
            return false;
        }

        if (!SymbolEqualityComparer.Default.Equals(
                model.GetSymbolInfo(element.Expression, cancellationToken).Symbol,
                listSymbol))
        {
            return false;
        }

        builder.Add(element);
        return true;
    }

    private static bool IsElementReceiver(IdentifierNameSyntax identifier)
    {
        return identifier.Parent is ElementAccessExpressionSyntax element
               && element.Expression.Span == identifier.Span;
    }

    private static bool IsNameChain(ExpressionSyntax expression)
    {
        return expression is IdentifierNameSyntax or MemberAccessExpressionSyntax
        {
            Expression: IdentifierNameSyntax or ThisExpressionSyntax or BaseExpressionSyntax
        };
    }

    private static bool IsWrite(ElementAccessExpressionSyntax element)
    {
        return element.Parent switch
        {
            AssignmentExpressionSyntax assignment => assignment.Left.Span == element.Span,
            PrefixUnaryExpressionSyntax prefix => prefix.Operand.Span == element.Span,
            PostfixUnaryExpressionSyntax postfix => postfix.Operand.Span == element.Span,
            ArgumentSyntax argument => argument.RefKindKeyword.IsKind(SyntaxKind.RefKeyword)
                                       || argument.RefKindKeyword.IsKind(SyntaxKind.OutKeyword),
            _ => false
        };
    }

    private static MemberAccessExpressionSyntax? GetRangeAccess(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression is MemberAccessExpressionSyntax memberAccess
               && string.Equals(memberAccess.Name.Identifier.Text, "GetRange", StringComparison.Ordinal)
               && invocation.ArgumentList.Arguments.Count is GetRangeArgumentCount
            ? memberAccess
            : null;
    }

    private static bool IsListMethod(IMethodSymbol methodSymbol, INamedTypeSymbol listType)
    {
        return methodSymbol.ContainingType is INamedTypeSymbol { IsGenericType: true } containingType
               && TypeSymbols.MatchesOriginalDefinition(containingType, listType);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol listType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (GetRangeAccess(invocation) is not { } memberAccess)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol methodSymbol
            || !IsListMethod(methodSymbol, listType))
        {
            return;
        }

        var convertedType = context.SemanticModel.GetTypeInfo(invocation, context.CancellationToken).ConvertedType;
        if (convertedType is INamedTypeSymbol converted
            && TypeSymbols.MatchesOriginalDefinition(converted, listType))
        {
            return;
        }

        if (context.SemanticModel.GetTypeInfo(memberAccess.Expression, context.CancellationToken).Nullability.FlowState
            is not NullableFlowState.NotNull)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
    }
}

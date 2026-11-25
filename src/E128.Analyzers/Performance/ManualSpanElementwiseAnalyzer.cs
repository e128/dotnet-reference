using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

/// <summary>
///     E128255: An indexed loop computes an element-wise sum or product of two spans one element at
///     a time. <c lang="csharp">TensorPrimitives</c> computes the block on the vector path.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ManualSpanElementwiseAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128255";
    internal const string AddOperation = "Add";
    internal const string MultiplyOperation = "Multiply";

    private const int ElementwiseParameterCount = 3;
    private const int FirstSourceParameter = 0;
    private const int SecondSourceParameter = 1;
    private const int DestinationParameter = 2;

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Use TensorPrimitives instead of an element-wise loop",
        "The loop computes one element at a time. Use TensorPrimitives.{0}.",
        "E128-Performance",
        DiagnosticSeverity.Warning,
        true,
        "A TensorPrimitives element-wise call reads the spans in vector-width blocks. The loop " +
        "computes one element per iteration and carries the loop overhead on every step.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static start =>
        {
            var elementwise = TryCreateContext(start.Compilation);
            if (elementwise is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(
                node => Analyze(node, elementwise),
                SyntaxKind.ForStatement);
        });
    }

    internal static ElementwiseContext? TryCreateContext(Compilation compilation)
    {
        var tensors = compilation.GetTypeByMetadataName("System.Numerics.Tensors.TensorPrimitives");
        var readOnlySpan = compilation.GetTypeByMetadataName("System.ReadOnlySpan`1");
        var span = compilation.GetTypeByMetadataName("System.Span`1");

        if (tensors is null || readOnlySpan is null || span is null)
        {
            return null;
        }

        var addTypes = SupportedTypes(compilation, tensors, readOnlySpan, span, AddOperation);
        var multiplyTypes = SupportedTypes(compilation, tensors, readOnlySpan, span, MultiplyOperation);

        return addTypes.IsEmpty && multiplyTypes.IsEmpty
            ? null
            : new ElementwiseContext(addTypes, multiplyTypes, readOnlySpan, span);
    }

    private static ImmutableArray<ITypeSymbol> SupportedTypes(
        Compilation compilation,
        INamedTypeSymbol tensors,
        INamedTypeSymbol readOnlySpan,
        INamedTypeSymbol span,
        string operation)
    {
        var types = ImmutableArray.CreateBuilder<ITypeSymbol>();

        foreach (var specialType in new[] { SpecialType.System_Single, SpecialType.System_Double })
        {
            var element = compilation.GetSpecialType(specialType);

            if (element.TypeKind is not TypeKind.Error
                && HasElementwise(tensors, readOnlySpan.Construct(element), span.Construct(element), operation))
            {
                types.Add(element);
            }
        }

        return types.ToImmutable();
    }

    private static bool HasElementwise(
        INamedTypeSymbol tensors,
        INamedTypeSymbol readOnlySpan,
        INamedTypeSymbol span,
        string operation)
    {
        return tensors.GetMembers(operation)
            .OfType<IMethodSymbol>()
            .Any(method => method.Parameters.Length is ElementwiseParameterCount
                           && SymbolEqualityComparer.Default.Equals(method.Parameters[FirstSourceParameter].Type, readOnlySpan)
                           && SymbolEqualityComparer.Default.Equals(method.Parameters[SecondSourceParameter].Type, readOnlySpan)
                           && SymbolEqualityComparer.Default.Equals(method.Parameters[DestinationParameter].Type, span));
    }

    private static void Analyze(SyntaxNodeAnalysisContext context, ElementwiseContext elementwise)
    {
        if (context.Node is not ForStatementSyntax forStatement)
        {
            return;
        }

        if (TryMatch(forStatement, context.SemanticModel, elementwise, context.CancellationToken) is not { } match)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, forStatement.GetLocation(), match.Operation));
    }

    internal static ElementwiseMatch? TryMatch(
        ForStatementSyntax forStatement,
        SemanticModel model,
        ElementwiseContext context,
        CancellationToken cancellationToken)
    {
        if (!IndexedForLoop.TryMatch(forStatement, out var index, out var bound, out var body) || bound is null)
        {
            return null;
        }

        if (GetParts(body, index, model, cancellationToken) is not { } parts)
        {
            return null;
        }

        if (!IsLengthDriver(bound, parts.Left) && !IsLengthDriver(bound, parts.Right))
        {
            return null;
        }

        if (!SupportsElementwise(parts, model, context, cancellationToken))
        {
            return null;
        }

        var firstIsLeft = IsLengthDriver(bound, parts.Left);

        return new ElementwiseMatch(
            parts.Operation,
            firstIsLeft ? parts.Left : parts.Right,
            firstIsLeft ? parts.Right : parts.Left,
            parts.Destination);
    }

    private static bool IsLengthDriver(ExpressionSyntax bound, ExpressionSyntax operand)
    {
        return SyntaxFactory.AreEquivalent(bound, operand);
    }

    private static LoopParts? GetParts(
        StatementSyntax body,
        string index,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        if (GetSingleAssignment(body) is not { } assignment)
        {
            return null;
        }

        if (assignment.Right is not BinaryExpressionSyntax binary)
        {
            return null;
        }

        if (OperationName(binary) is not { } operation)
        {
            return null;
        }

        if (!TryGetOperand(assignment.Left, index, model, cancellationToken, out var destination))
        {
            return null;
        }

        if (!TryGetOperand(binary.Left, index, model, cancellationToken, out var left))
        {
            return null;
        }

        return TryGetOperand(binary.Right, index, model, cancellationToken, out var right)
            ? new LoopParts(operation, destination, left, right)
            : null;
    }

    private static bool SupportsElementwise(
        LoopParts parts,
        SemanticModel model,
        ElementwiseContext context,
        CancellationToken cancellationToken)
    {
        if (!TryGetElementType(parts.Destination, model, context, cancellationToken, out var destinationElement))
        {
            return false;
        }

        if (!TryGetElementType(parts.Left, model, context, cancellationToken, out var leftElement))
        {
            return false;
        }

        if (!TryGetElementType(parts.Right, model, context, cancellationToken, out var rightElement))
        {
            return false;
        }

        if (leftElement is null
            || rightElement is null
            || !SymbolEqualityComparer.Default.Equals(leftElement, rightElement)
            || !SymbolEqualityComparer.Default.Equals(leftElement, destinationElement))
        {
            return false;
        }

        return context.Supports(parts.Operation, leftElement);
    }

    private static string? OperationName(BinaryExpressionSyntax binary)
    {
        if (binary.IsKind(SyntaxKind.AddExpression))
        {
            return AddOperation;
        }

        return binary.IsKind(SyntaxKind.MultiplyExpression) ? MultiplyOperation : null;
    }

    private static bool TryGetElementType(
        ExpressionSyntax receiver,
        SemanticModel model,
        ElementwiseContext context,
        CancellationToken cancellationToken,
        out ITypeSymbol? element)
    {
        element = null;

        return SpanElementType.TryGet(
            model.GetTypeInfo(receiver, cancellationToken).Type,
            context.Span,
            context.ReadOnlySpan,
            out element,
            out _);
    }

    private static AssignmentExpressionSyntax? GetSingleAssignment(StatementSyntax body)
    {
        var statement = body is BlockSyntax { Statements.Count: 1 } block ? block.Statements[0] : body;

        return statement is ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assignment }
               && assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
            ? assignment
            : null;
    }

    private static bool TryGetOperand(
        ExpressionSyntax? expression,
        string index,
        SemanticModel model,
        CancellationToken cancellationToken,
        [NotNullWhen(true)] out ExpressionSyntax? receiver)
    {
        receiver = null;

        if (expression is not ElementAccessExpressionSyntax { ArgumentList.Arguments.Count: 1 } elementAccess
            || elementAccess.ArgumentList.Arguments[0].Expression is not IdentifierNameSyntax indexName
            || !string.Equals(indexName.Identifier.ValueText, index, StringComparison.Ordinal))
        {
            return false;
        }

        var candidate = elementAccess.Expression;

        if (candidate.DescendantNodesAndSelf().Any(static node => node is InvocationExpressionSyntax)
            || model.GetSymbolInfo(candidate, cancellationToken).Symbol is not (ILocalSymbol or IParameterSymbol or IFieldSymbol))
        {
            return false;
        }

        receiver = candidate;
        return true;
    }

    private sealed class LoopParts
    {
        public LoopParts(
            string operation,
            ExpressionSyntax destination,
            ExpressionSyntax left,
            ExpressionSyntax right)
        {
            Operation = operation;
            Destination = destination;
            Left = left;
            Right = right;
        }

        public string Operation { get; }

        public ExpressionSyntax Destination { get; }

        public ExpressionSyntax Left { get; }

        public ExpressionSyntax Right { get; }
    }

    internal sealed class ElementwiseContext
    {
        public ElementwiseContext(
            ImmutableArray<ITypeSymbol> addTypes,
            ImmutableArray<ITypeSymbol> multiplyTypes,
            INamedTypeSymbol readOnlySpan,
            INamedTypeSymbol span)
        {
            AddTypes = addTypes;
            MultiplyTypes = multiplyTypes;
            ReadOnlySpan = readOnlySpan;
            Span = span;
        }

        public ImmutableArray<ITypeSymbol> AddTypes { get; }

        public ImmutableArray<ITypeSymbol> MultiplyTypes { get; }

        public INamedTypeSymbol ReadOnlySpan { get; }

        public INamedTypeSymbol Span { get; }

        internal bool Supports(string operation, ITypeSymbol element)
        {
            var types = string.Equals(operation, AddOperation, StringComparison.Ordinal) ? AddTypes : MultiplyTypes;

            return types.Contains(element, SymbolEqualityComparer.Default);
        }
    }

    internal sealed class ElementwiseMatch
    {
        public ElementwiseMatch(
            string operation,
            ExpressionSyntax first,
            ExpressionSyntax second,
            ExpressionSyntax destination)
        {
            Operation = operation;
            First = first;
            Second = second;
            Destination = destination;
        }

        public string Operation { get; }

        public ExpressionSyntax First { get; }

        public ExpressionSyntax Second { get; }

        public ExpressionSyntax Destination { get; }
    }
}

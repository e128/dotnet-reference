using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

/// <summary>
///     E128135: A loop reduces the elements of a float or double sequence one at a time.
///     <c lang="csharp">TensorPrimitives</c> uses the vector path.
/// </summary>
/// <remarks>
///     The rule covers a sum loop and a min or max loop that keeps a neutral initial value.
///     A minimum or maximum loop whose accumulator starts at any other value keeps a value that
///     <c lang="csharp">TensorPrimitives</c> would replace, so the rule stays silent there.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ManualSpanSumAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128135";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Use TensorPrimitives instead of a reduction loop",
        "The loop reduces the elements one at a time. Use TensorPrimitives.{0}.",
        "E128-Performance",
        DiagnosticSeverity.Warning,
        true,
        "A TensorPrimitives reduction reads the span in vector-width blocks. The loop reduces one " +
        "element per iteration and carries a loop overhead on every step.");

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
            var tensors = start.Compilation.GetTypeByMetadataName("System.Numerics.Tensors.TensorPrimitives");
            var readOnlySpan = start.Compilation.GetTypeByMetadataName("System.ReadOnlySpan`1");
            var span = start.Compilation.GetTypeByMetadataName("System.Span`1");

            if (tensors is null || readOnlySpan is null || span is null)
            {
                return;
            }

            var floatType = start.Compilation.GetSpecialType(SpecialType.System_Single);
            var doubleType = start.Compilation.GetSpecialType(SpecialType.System_Double);

            if (floatType.TypeKind is TypeKind.Error || doubleType.TypeKind is TypeKind.Error)
            {
                return;
            }

            var supported = new ITypeSymbol[] { floatType, doubleType }
                .Where(type => HasReduction(tensors, readOnlySpan.Construct(type), "Sum"))
                .ToImmutableArray();

            if (supported.IsEmpty)
            {
                return;
            }

            var context2 = new SumContext(supported, tensors, readOnlySpan, span);

            start.RegisterSyntaxNodeAction(
                node => Analyze(node, context2),
                SyntaxKind.ForEachStatement);
        });
    }

    private static void Analyze(SyntaxNodeAnalysisContext context, SumContext sumContext)
    {
        if (context.Node is not ForEachStatementSyntax loop
            || !TryMatchLoop(loop, out var operation, out var accumulatorName, out _))
        {
            return;
        }

        var model = context.SemanticModel;

        if (FindAccumulatorType(model, loop, accumulatorName, sumContext, context.CancellationToken) is not { } accumulatorType)
        {
            return;
        }

        if (operation is not SpanOperation.Sum
            && (!HasReduction(sumContext.Tensors, sumContext.ReadOnlySpan.Construct(accumulatorType), OperationName(operation))
                || !HasNeutralInitializer(loop, accumulatorName, accumulatorType, operation, model, context.CancellationToken)))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, loop.GetLocation(), OperationName(operation)));
    }

    private static ITypeSymbol? FindAccumulatorType(
        SemanticModel model,
        ForEachStatementSyntax loop,
        string accumulatorName,
        SumContext sumContext,
        System.Threading.CancellationToken cancellationToken)
    {
        if (ManualBitCountAnalyzer.FindType(model, loop, accumulatorName, cancellationToken) is not { } accumulatorType)
        {
            return null;
        }

        if (accumulatorType.SpecialType is not (SpecialType.System_Single or SpecialType.System_Double))
        {
            return null;
        }

        if (!sumContext.SupportedTypes.Contains(accumulatorType, SymbolEqualityComparer.Default))
        {
            return null;
        }

        return GetElementType(model, loop.Expression, sumContext, cancellationToken) is { } elementType
               && SymbolEqualityComparer.Default.Equals(elementType, accumulatorType)
            ? accumulatorType
            : null;
    }

    /// <summary>
    ///     Matches a foreach loop whose whole body reduces the element into a local. The analyzer and
    ///     the fix provider call this one helper.
    /// </summary>
    internal static bool TryMatchLoop(
        ForEachStatementSyntax loop,
        out SpanOperation operation,
        out string accumulatorName,
        out string valueName)
    {
        operation = SpanOperation.Sum;
        accumulatorName = string.Empty;
        valueName = loop.Identifier.ValueText;

        if (TryMatchSum(loop.Statement, valueName, out accumulatorName))
        {
            return true;
        }

        if (TryMatchMinOrMax(loop.Statement, valueName, out operation, out accumulatorName))
        {
            return true;
        }

        operation = SpanOperation.Sum;
        return false;
    }

    private static bool TryMatchSum(StatementSyntax statement, string valueName, out string accumulatorName)
    {
        accumulatorName = string.Empty;
        statement = UnwrapSingleStatement(statement);

        if (statement is not ExpressionStatementSyntax expression)
        {
            return false;
        }

        if (expression.Expression is not AssignmentExpressionSyntax add
            || !add.IsKind(SyntaxKind.AddAssignmentExpression))
        {
            return false;
        }

        if (add.Left is not IdentifierNameSyntax accumulator || add.Right is not IdentifierNameSyntax value)
        {
            return false;
        }

        if (!string.Equals(value.Identifier.ValueText, valueName, StringComparison.Ordinal))
        {
            return false;
        }

        accumulatorName = accumulator.Identifier.ValueText;
        return true;
    }

    private static bool TryMatchMinOrMax(
        StatementSyntax statement,
        string valueName,
        out SpanOperation operation,
        out string accumulatorName)
    {
        operation = SpanOperation.Sum;
        accumulatorName = string.Empty;
        statement = UnwrapSingleStatement(statement);

        if (statement is not IfStatementSyntax { Else: null } conditional)
        {
            return false;
        }

        if (conditional.Condition is not BinaryExpressionSyntax comparison
            || comparison.Right is not IdentifierNameSyntax comparedAccumulator)
        {
            return false;
        }

        if (!IsValueOperand(comparison.Left, valueName) || !TryGetOperation(comparison, out operation))
        {
            return false;
        }

        if (!TryGetAssignment(conditional.Statement, out var writtenAccumulator, out var writtenValue))
        {
            return false;
        }

        if (!string.Equals(writtenAccumulator, comparedAccumulator.Identifier.ValueText, StringComparison.Ordinal)
            || !string.Equals(writtenValue, valueName, StringComparison.Ordinal))
        {
            return false;
        }

        accumulatorName = comparedAccumulator.Identifier.ValueText;
        return true;
    }

    private static StatementSyntax UnwrapSingleStatement(StatementSyntax statement)
    {
        return statement is BlockSyntax { Statements.Count: 1 } block ? block.Statements[0] : statement;
    }

    private static bool IsValueOperand(ExpressionSyntax expression, string valueName)
    {
        return expression is IdentifierNameSyntax identifier
               && string.Equals(identifier.Identifier.ValueText, valueName, StringComparison.Ordinal);
    }

    private static bool TryGetOperation(BinaryExpressionSyntax comparison, out SpanOperation operation)
    {
        if (comparison.IsKind(SyntaxKind.GreaterThanExpression))
        {
            operation = SpanOperation.Max;
            return true;
        }

        if (comparison.IsKind(SyntaxKind.LessThanExpression))
        {
            operation = SpanOperation.Min;
            return true;
        }

        operation = SpanOperation.Sum;
        return false;
    }

    private static bool TryGetAssignment(StatementSyntax statement, out string accumulatorName, out string valueName)
    {
        accumulatorName = string.Empty;
        valueName = string.Empty;
        statement = UnwrapSingleStatement(statement);

        if (statement is not ExpressionStatementSyntax expression
            || expression.Expression is not AssignmentExpressionSyntax write)
        {
            return false;
        }

        if (write.Left is not IdentifierNameSyntax accumulator || write.Right is not IdentifierNameSyntax value)
        {
            return false;
        }

        accumulatorName = accumulator.Identifier.ValueText;
        valueName = value.Identifier.ValueText;
        return true;
    }

    private static string OperationName(SpanOperation operation)
    {
        if (operation is SpanOperation.Max)
        {
            return "Max";
        }

        return operation is SpanOperation.Min ? "Min" : "Sum";
    }

    /// <summary>
    ///     True when the accumulator starts at the value that the reduction leaves unchanged, so
    ///     the vector call computes the same result as the loop.
    /// </summary>
    private static bool HasNeutralInitializer(
        ForEachStatementSyntax loop,
        string accumulatorName,
        ITypeSymbol accumulatorType,
        SpanOperation operation,
        SemanticModel model,
        System.Threading.CancellationToken cancellationToken)
    {
        if (loop.Parent is not BlockSyntax block)
        {
            return false;
        }

        for (var i = block.Statements.IndexOf(loop) - 1; i >= 0; i--)
        {
            var statement = block.Statements[i];

            if (statement is LocalDeclarationStatementSyntax declaration
                && FindDeclarator(declaration, accumulatorName) is { } declarator)
            {
                return IsNeutralValue(declarator, accumulatorType, operation, model, cancellationToken);
            }

            if (WritesTo(statement, accumulatorName))
            {
                return false;
            }
        }

        return false;
    }

    private static bool IsNeutralValue(
        VariableDeclaratorSyntax declarator,
        ITypeSymbol accumulatorType,
        SpanOperation operation,
        SemanticModel model,
        System.Threading.CancellationToken cancellationToken)
    {
        if (declarator.Initializer?.Value is not { } initializer)
        {
            return false;
        }

        var expectedField = operation is SpanOperation.Max ? "MinValue" : "MaxValue";

        return model.GetSymbolInfo(initializer, cancellationToken).Symbol is IFieldSymbol field
               && string.Equals(field.Name, expectedField, StringComparison.Ordinal)
               && SymbolEqualityComparer.Default.Equals(field.ContainingType, accumulatorType);
    }

    private static VariableDeclaratorSyntax? FindDeclarator(
        LocalDeclarationStatementSyntax declaration,
        string name)
    {
        return declaration.Declaration.Variables
            .FirstOrDefault(variable => string.Equals(variable.Identifier.ValueText, name, StringComparison.Ordinal));
    }

    private static bool WritesTo(StatementSyntax statement, string name)
    {
        foreach (var identifier in statement.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
        {
            if (!string.Equals(identifier.Identifier.ValueText, name, StringComparison.Ordinal))
            {
                continue;
            }

            if (IsWriteTarget(identifier))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsWriteTarget(IdentifierNameSyntax identifier)
    {
        return identifier.Parent switch
        {
            AssignmentExpressionSyntax assignment => assignment.Left == identifier,
            PrefixUnaryExpressionSyntax prefix => prefix.Operand == identifier,
            PostfixUnaryExpressionSyntax postfix => postfix.Operand == identifier,
            ArgumentSyntax argument => argument.RefKindKeyword.IsKind(SyntaxKind.RefKeyword)
                                       || argument.RefKindKeyword.IsKind(SyntaxKind.OutKeyword),
            _ => false
        };
    }

    internal static ITypeSymbol? GetElementType(
        SemanticModel model,
        ExpressionSyntax expression,
        SumContext sumContext,
        System.Threading.CancellationToken cancellationToken)
    {
        return SpanElementType.TryGet(
            model.GetTypeInfo(expression, cancellationToken).Type,
            sumContext.Span,
            sumContext.ReadOnlySpan,
            out var element,
            out _)
            ? element
            : null;
    }

    private static bool HasReduction(INamedTypeSymbol tensors, INamedTypeSymbol spanType, string name)
    {
        return tensors.GetMembers(name)
            .OfType<IMethodSymbol>()
            .Any(method => method.Parameters.Length is 1
                && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, spanType));
    }

    internal sealed class SumContext
    {
        public SumContext(
            ImmutableArray<ITypeSymbol> supportedTypes,
            INamedTypeSymbol tensors,
            INamedTypeSymbol readOnlySpan,
            INamedTypeSymbol span)
        {
            SupportedTypes = supportedTypes;
            Tensors = tensors;
            ReadOnlySpan = readOnlySpan;
            Span = span;
        }

        public ImmutableArray<ITypeSymbol> SupportedTypes { get; }

        public INamedTypeSymbol Tensors { get; }

        public INamedTypeSymbol ReadOnlySpan { get; }

        public INamedTypeSymbol Span { get; }
    }
}

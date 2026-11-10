using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

/// <summary>
///     E128140: A <c lang="csharp">for</c> loop that writes one element per iteration reimplements
///     <c lang="csharp">Span.Fill</c>, <c lang="csharp">Span.Clear</c>, or
///     <c lang="csharp">Span.CopyTo</c>. The span method copies the block in one bounds-checked call.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ElementLoopToBulkSpanAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128140";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Use a span bulk method instead of an element loop",
        "This loop copies one element at a time. {0} moves the block in one bounds-checked call.",
        "E128-Performance",
        DiagnosticSeverity.Warning,
        true);

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
            var spanType = start.Compilation.GetTypeByMetadataName("System.Span`1");
            if (spanType is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(
                node => Analyze(node, spanType),
                SyntaxKind.ForStatement);
        });
    }

    private static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol spanType)
    {
        var forStatement = (ForStatementSyntax)context.Node;
        var match = TryMatch(forStatement, context.SemanticModel, spanType, context.CancellationToken);
        if (match is null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            forStatement.GetLocation(),
            match.BulkMethod));
    }

    internal static BulkLoopMatch? TryMatch(
        ForStatementSyntax forStatement,
        SemanticModel model,
        INamedTypeSymbol spanType,
        CancellationToken cancellationToken)
    {
        if (!IndexedForLoop.TryMatch(forStatement, out var variable, out var bound, out var body)
            || bound is null)
        {
            return null;
        }

        if (GetSimpleAssignment(body) is not { } assignment
            || GetSimpleDestination(assignment, variable, model, cancellationToken) is not { } destination)
        {
            return null;
        }

        var destinationType = model.GetTypeInfo(destination, cancellationToken).Type;
        if (!TryGetElementType(destinationType, spanType, out var destinationElement, out var destinationIsSpan))
        {
            return null;
        }

        if (TryGetIndexedReceiver(assignment.Right, variable, out var source) && source is not null)
        {
            return TryMatchCopy(source, variable, destinationElement, bound, model, spanType, cancellationToken)
                is { } sourceMatch
                ? new BulkLoopMatch(LoopKind.Copy, destination, null, source, destinationIsSpan, sourceMatch)
                : null;
        }

        return IsFillCandidate(destination, bound, assignment.Right, variable, model, cancellationToken)
            ? CreateFillMatch(destination, assignment.Right, destinationElement, destinationIsSpan, model, cancellationToken)
            : null;
    }

    private static AssignmentExpressionSyntax? GetSimpleAssignment(StatementSyntax body)
    {
        return GetSingleStatement(body) is ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assignment }
            && assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
            ? assignment
            : null;
    }

    private static ExpressionSyntax? GetSimpleDestination(
        AssignmentExpressionSyntax assignment,
        string variable,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        return TryGetIndexedReceiver(assignment.Left, variable, out var destination)
            && destination is not null
            && IsSimpleTarget(destination, variable, model, cancellationToken)
            ? destination
            : null;
    }

    // Fill evaluates the value once, so a call or a property read would run fewer times.
    private static bool IsFillCandidate(
        ExpressionSyntax destination,
        ExpressionSyntax bound,
        ExpressionSyntax value,
        string variable,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        return SyntaxFactory.AreEquivalent(destination, bound)
            && !References(value, variable)
            && IsRepeatableValue(value, model, cancellationToken);
    }

    private static BulkLoopMatch CreateFillMatch(
        ExpressionSyntax destination,
        ExpressionSyntax value,
        ITypeSymbol? destinationElement,
        bool destinationIsSpan,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        return IsClearValue(value, destinationElement, model, cancellationToken)
            ? new BulkLoopMatch(LoopKind.Clear, destination, null, null, destinationIsSpan, false)
            : new BulkLoopMatch(LoopKind.Fill, destination, value, null, destinationIsSpan, false);
    }

    // Returns whether the source is already a span when the loop is a valid copy.
    private static bool? TryMatchCopy(
        ExpressionSyntax source,
        string variable,
        ITypeSymbol? destinationElement,
        ExpressionSyntax bound,
        SemanticModel model,
        INamedTypeSymbol spanType,
        CancellationToken cancellationToken)
    {
        bool? isSpan = null;

        // CopyTo reads the whole source span, so the loop must iterate the source length.
        if (IsSimpleTarget(source, variable, model, cancellationToken)
            && SyntaxFactory.AreEquivalent(source, bound)
            && TryGetElementType(
                model.GetTypeInfo(source, cancellationToken).Type,
                spanType,
                out var sourceElement,
                out var sourceIsSpan)
            && SymbolEqualityComparer.Default.Equals(sourceElement, destinationElement))
        {
            isSpan = sourceIsSpan;
        }

        return isSpan;
    }

    private static StatementSyntax? GetSingleStatement(StatementSyntax statement)
    {
        if (statement is not BlockSyntax block)
        {
            return statement;
        }

        return block.Statements.Count is 1 ? block.Statements[0] : null;
    }

    private static bool TryGetIndexedReceiver(
        ExpressionSyntax expression,
        string variable,
        out ExpressionSyntax? receiver)
    {
        receiver = null;

        if (expression is not ElementAccessExpressionSyntax { ArgumentList.Arguments.Count: 1 } elementAccess
            || elementAccess.ArgumentList.Arguments[0].Expression is not IdentifierNameSyntax index
            || !string.Equals(index.Identifier.ValueText, variable, StringComparison.Ordinal))
        {
            return false;
        }

        receiver = elementAccess.Expression;
        return true;
    }

    // The fix evaluates the target once, so it must be a plain variable. A call, a property
    // read, or a reference to the loop variable would change meaning.
    private static bool IsSimpleTarget(
        ExpressionSyntax expression,
        string variable,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        return !References(expression, variable)
            && !expression.DescendantNodesAndSelf().Any(static node => node is InvocationExpressionSyntax)
            && model.GetSymbolInfo(expression, cancellationToken).Symbol
            is ILocalSymbol or IParameterSymbol or IFieldSymbol;
    }

    private static bool TryGetElementType(
        ITypeSymbol? type,
        INamedTypeSymbol spanType,
        out ITypeSymbol? element,
        out bool isSpan)
    {
        element = null;
        isSpan = false;

        switch (type)
        {
            case IArrayTypeSymbol { Rank: 1 } array:
                element = array.ElementType;
                return true;

            case INamedTypeSymbol named
                when TypeSymbols.MatchesOriginalDefinition(named, spanType):
                element = named.TypeArguments[0];
                isSpan = true;
                return true;

            default:
                return false;
        }
    }

    private static bool IsRepeatableValue(
        ExpressionSyntax expression,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        return expression switch
        {
            LiteralExpressionSyntax => true,
            DefaultExpressionSyntax => true,
            IdentifierNameSyntax identifier => IsVariable(model.GetSymbolInfo(identifier, cancellationToken).Symbol),
            MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } memberAccess =>
                model.GetSymbolInfo(memberAccess, cancellationToken).Symbol is IFieldSymbol,
            _ => false,
        };
    }

    private static bool IsVariable(ISymbol? symbol)
    {
        return symbol is ILocalSymbol or IParameterSymbol or IFieldSymbol;
    }

    private static bool References(ExpressionSyntax expression, string variable)
    {
        return expression.DescendantNodesAndSelf()
            .OfType<IdentifierNameSyntax>()
            .Any(identifier => string.Equals(identifier.Identifier.ValueText, variable, StringComparison.Ordinal));
    }

    // Clear writes default(T). A zero literal is not default(T) for a nullable element, an
    // object element, or a type that a user-defined conversion would reach.
    private static bool IsClearValue(
        ExpressionSyntax value,
        ITypeSymbol? element,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        if (value is DefaultExpressionSyntax)
        {
            return true;
        }

        if (value is LiteralExpressionSyntax literal && ClassifyLiteral(literal, element) is { } literalResult)
        {
            return literalResult;
        }

        if (element is null || !IsZeroableValueType(element))
        {
            return false;
        }

        var conversion = model.ClassifyConversion(value, element);
        if (!conversion.IsImplicit || conversion.IsUserDefined)
        {
            return false;
        }

        var constant = model.GetConstantValue(value, cancellationToken);
        return constant.HasValue && IsZero(constant.Value);
    }

    // A default literal clears. A null literal clears only a reference or nullable element.
    // Any other literal is undecided, so the caller keeps checking.
    private static bool? ClassifyLiteral(LiteralExpressionSyntax literal, ITypeSymbol? element)
    {
        if (literal.IsKind(SyntaxKind.DefaultLiteralExpression))
        {
            return true;
        }

        return literal.IsKind(SyntaxKind.NullLiteralExpression)
            ? element is null || !element.IsValueType || IsNullable(element)
            : null;
    }

    private static bool IsZeroableValueType(ITypeSymbol element)
    {
        return element.IsValueType
            && !IsNullable(element)
            && element.IsUnmanagedType;
    }

    private static bool IsZero(object? value)
    {
        return value switch
        {
            bool flag => !flag,
            char character => character == '\0',
            float number => number == 0,
            double number => number == 0,
            decimal number => number == 0,
            _ => IsZeroInteger(value),
        };
    }

    private static bool IsZeroInteger(object? value)
    {
        return value switch
        {
            byte number => number is 0,
            sbyte number => number is 0,
            short number => number is 0,
            ushort number => number is 0,
            int number => number is 0,
            uint number => number == 0,
            long number => number == 0,
            ulong number => number == 0,
            _ => false,
        };
    }

    private static bool IsNullable(ITypeSymbol type)
    {
        return type.OriginalDefinition.SpecialType is SpecialType.System_Nullable_T;
    }

    internal enum LoopKind
    {
        Fill = 0,
        Clear = 1,
        Copy = 2,
    }

    internal sealed class BulkLoopMatch
    {
        public BulkLoopMatch(
            LoopKind kind,
            ExpressionSyntax destination,
            ExpressionSyntax? value,
            ExpressionSyntax? source,
            bool destinationIsSpan,
            bool sourceIsSpan)
        {
            Kind = kind;
            Destination = destination;
            Value = value;
            Source = source;
            DestinationIsSpan = destinationIsSpan;
            SourceIsSpan = sourceIsSpan;
        }

        public LoopKind Kind { get; }

        public ExpressionSyntax Destination { get; }

        public ExpressionSyntax? Value { get; }

        public ExpressionSyntax? Source { get; }

        public bool DestinationIsSpan { get; }

        public bool SourceIsSpan { get; }

        public string BulkMethod => Kind switch
        {
            LoopKind.Fill => "Fill",
            LoopKind.Clear => "Clear",
            LoopKind.Copy => "CopyTo",
            _ => "CopyTo",
        };
    }
}

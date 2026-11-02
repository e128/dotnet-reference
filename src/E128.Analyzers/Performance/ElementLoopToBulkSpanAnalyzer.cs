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
        "Performance",
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
        context.RegisterCompilationStartAction(start =>
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
        if (!TryGetLoopShape(forStatement, out var variable, out var bound, out var body)
            || bound is null
            || body is null)
        {
            return null;
        }

        if (GetSingleStatement(body) is not ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assignment }
            || !assignment.IsKind(SyntaxKind.SimpleAssignmentExpression))
        {
            return null;
        }

        if (!TryGetIndexedReceiver(assignment.Left, variable, out var destination)
            || destination is null
            || !IsSimpleTarget(destination, variable, model, cancellationToken))
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

        BulkLoopMatch? fill = null;

        // Fill evaluates the value once, so a call or a property read would run fewer times.
        if (SyntaxFactory.AreEquivalent(destination, bound)
            && !References(assignment.Right, variable)
            && IsRepeatableValue(assignment.Right, model, cancellationToken))
        {
            fill = IsClearValue(assignment.Right, destinationElement, model)
                ? new BulkLoopMatch(LoopKind.Clear, destination, null, null, destinationIsSpan, false)
                : new BulkLoopMatch(LoopKind.Fill, destination, assignment.Right, null, destinationIsSpan, false);
        }

        return fill;
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

    private static bool TryGetLoopShape(
        ForStatementSyntax forStatement,
        out string variable,
        out ExpressionSyntax? bound,
        out StatementSyntax? body)
    {
        variable = string.Empty;
        bound = null;
        body = null;

        if (forStatement.Declaration is not { Variables.Count: 1 } declaration)
        {
            return false;
        }

        var declarator = declaration.Variables[0];

        if (declarator.Initializer?.Value is not LiteralExpressionSyntax initial
            || !initial.IsKind(SyntaxKind.NumericLiteralExpression)
            || !string.Equals(initial.Token.ValueText, "0", StringComparison.Ordinal)
            || forStatement.Condition is not BinaryExpressionSyntax condition
            || !condition.IsKind(SyntaxKind.LessThanExpression)
            || condition.Left is not IdentifierNameSyntax conditionVariable
            || condition.Right is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Length" } length
            || forStatement.Incrementors.Count != 1
            || forStatement.Incrementors[0] is not PostfixUnaryExpressionSyntax increment
            || !increment.IsKind(SyntaxKind.PostIncrementExpression)
            || increment.Operand is not IdentifierNameSyntax incrementVariable
            || !string.Equals(declarator.Identifier.ValueText, conditionVariable.Identifier.ValueText, StringComparison.Ordinal)
            || !string.Equals(declarator.Identifier.ValueText, incrementVariable.Identifier.ValueText, StringComparison.Ordinal))
        {
            return false;
        }

        variable = declarator.Identifier.ValueText;
        bound = length.Expression;
        body = forStatement.Statement;
        return true;
    }

    private static StatementSyntax? GetSingleStatement(StatementSyntax statement)
    {
        return statement is BlockSyntax block
            ? block.Statements.Count == 1 ? block.Statements[0] : null
            : statement;
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
            && !expression.DescendantNodesAndSelf().Any(node => node is InvocationExpressionSyntax)
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
                when SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, spanType):
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
    private static bool IsClearValue(ExpressionSyntax value, ITypeSymbol? element, SemanticModel model)
    {
        if (value is DefaultExpressionSyntax)
        {
            return true;
        }

        if (value is LiteralExpressionSyntax literal)
        {
            if (literal.IsKind(SyntaxKind.DefaultLiteralExpression))
            {
                return true;
            }

            if (literal.IsKind(SyntaxKind.NullLiteralExpression))
            {
                return element is null || !element.IsValueType || IsNullable(element);
            }
        }

        if (element is null
            || !element.IsValueType
            || IsNullable(element)
            || !element.IsUnmanagedType)
        {
            return false;
        }

        var conversion = model.ClassifyConversion(value, element);
        if (!conversion.IsImplicit || conversion.IsUserDefined)
        {
            return false;
        }

        var constant = model.GetConstantValue(value);
        return constant.HasValue && IsZero(constant.Value);
    }

    private static bool IsZero(object? value)
    {
        return value switch
        {
            bool flag => !flag,
            char character => character == '\0',
            byte number => number == 0,
            sbyte number => number == 0,
            short number => number == 0,
            ushort number => number == 0,
            int number => number == 0,
            uint number => number == 0,
            long number => number == 0,
            ulong number => number == 0,
            float number => number == 0,
            double number => number == 0,
            decimal number => number == 0,
            _ => false,
        };
    }

    private static bool IsNullable(ITypeSymbol type)
    {
        return type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
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

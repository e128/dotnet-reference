using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Security;

/// <summary>
///     E128227: Reports an <c lang="csharp">HttpContent</c> body read with no size bound.
///     <c lang="csharp">ReadAsStringAsync</c> and <c lang="csharp">ReadAsByteArrayAsync</c> buffer
///     the whole body in memory, so an untrusted server can exhaust the heap.
/// </summary>
/// <remarks>
///     The rule resolves symbols instead of matching names. A bound must precede the read and
///     dominate it. A bound is a resolved <c lang="csharp">HttpContent.LoadIntoBufferAsync</c>
///     call with the <c lang="csharp">long</c> cap overload, or a null-safe
///     <c lang="csharp">ContentLength</c> comparison on the same content chain. A plain
///     greater-than guard does not bound the read, because <c lang="csharp">ContentLength</c> is
///     <c lang="csharp">null</c> for a chunked response and the guard passes through. A
///     <c lang="csharp">MaxResponseContentBufferSize</c> setting anywhere in the type bounds it
///     too. An in-memory content type needs no bound, because its size is already known. The
///     rule carries no code fix, because the cap value is a caller decision.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HttpBodyReadWithoutSizeBoundAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128227";

    private const string HttpContentTypeName = "System.Net.Http.HttpContent";

    private const string HttpContentHeadersTypeName = "System.Net.Http.Headers.HttpContentHeaders";

    private const string ByteArrayContentTypeName = "System.Net.Http.ByteArrayContent";

    private const string ReadOnlyMemoryContentTypeName = "System.Net.Http.ReadOnlyMemoryContent";

    private const string JsonContentTypeName = "System.Net.Http.Json.JsonContent";

    private const string CappedBufferLoadName = "LoadIntoBufferAsync";

    private const string ContentLengthName = "ContentLength";

    private static readonly ImmutableHashSet<string> ReadMethodNames = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "ReadAsStringAsync",
        "ReadAsByteArrayAsync");

    private static readonly ImmutableHashSet<string> TypeBoundNames = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "MaxResponseContentBufferSize");

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "HTTP body read without a size bound",
        "'{0}' buffers the whole body with no size bound. Call LoadIntoBufferAsync with a cap, add a null-safe ContentLength check before the read, or set MaxResponseContentBufferSize.",
        "E128-Security",
        DiagnosticSeverity.Warning,
        true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static startContext =>
        {
            // The rule names an HttpContent member. Without that type there is no report to make.
            if (startContext.Compilation.GetTypeByMetadataName(HttpContentTypeName) is not { } httpContent)
            {
                return;
            }

            INamedTypeSymbol?[] inMemoryContents =
            [
                startContext.Compilation.GetTypeByMetadataName(ByteArrayContentTypeName),
                startContext.Compilation.GetTypeByMetadataName(ReadOnlyMemoryContentTypeName),
                startContext.Compilation.GetTypeByMetadataName(JsonContentTypeName)
            ];

            var httpContentHeaders = startContext.Compilation.GetTypeByMetadataName(HttpContentHeadersTypeName);

            startContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeRead(nodeContext, httpContent, httpContentHeaders, inMemoryContents),
                SyntaxKind.InvocationExpression);
        });
    }

    private static void AnalyzeRead(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol httpContent,
        INamedTypeSymbol? httpContentHeaders,
        INamedTypeSymbol?[] inMemoryContents)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax access
            || !ReadMethodNames.Contains(access.Name.Identifier.ValueText))
        {
            return;
        }

        var model = context.SemanticModel;
        var cancellationToken = context.CancellationToken;

        if (model.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol method
            || !TypeSymbols.DerivesFrom(method.ContainingType, httpContent))
        {
            return;
        }

        var receiverType = model.GetTypeInfo(access.Expression, cancellationToken).Type;
        if (IsInMemoryContent(receiverType, inMemoryContents)
            || HasSizeBound(invocation, model, httpContent, httpContentHeaders, cancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), method.Name));
    }

    private static bool IsInMemoryContent(ITypeSymbol? type, INamedTypeSymbol?[] inMemoryContents)
    {
        foreach (var content in inMemoryContents)
        {
            if (TypeSymbols.DerivesFrom(type, content))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     True when a bound precedes the read and dominates it, or when the enclosing type
    ///     configures the client cap. A bound is resolved by symbol. A bound in another method
    ///     cannot be proven to apply to this read, so only the client cap is trusted at type scope.
    /// </summary>
    private static bool HasSizeBound(
        InvocationExpressionSyntax read,
        SemanticModel model,
        INamedTypeSymbol httpContent,
        INamedTypeSymbol? httpContentHeaders,
        CancellationToken cancellationToken)
    {
        var scope = BlockScope.GetScope(read);
        if (scope is not null)
        {
            var readRoot = RootName(read.Expression);
            if (readRoot.Length > 0)
            {
                var contentLength = GetContentLengthProperty(httpContentHeaders);
                var boundVariables = CollectBoundVariables(scope, model, contentLength, readRoot, cancellationToken);
                if (HasCappedBufferLoad(scope, read, readRoot, model, httpContent, cancellationToken)
                    || HasContentLengthBound(scope, read, readRoot, model, contentLength, boundVariables, cancellationToken))
                {
                    return true;
                }
            }
        }

        return read.FirstAncestorOrSelf<TypeDeclarationSyntax>() is { } type
               && Mentions(type, TypeBoundNames);
    }

    /// <summary>
    ///     True when a resolved <c lang="csharp">LoadIntoBufferAsync</c> call on the same content
    ///     chain uses the cap overload with a <c lang="csharp">long</c> argument. The
    ///     parameterless overload reads the whole body and does not bound anything.
    /// </summary>
    private static bool HasCappedBufferLoad(
        SyntaxNode scope,
        InvocationExpressionSyntax read,
        string readRoot,
        SemanticModel model,
        INamedTypeSymbol httpContent,
        CancellationToken cancellationToken)
    {
        foreach (var candidate in scope.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (candidate != read
                && SameRoot(candidate.Expression, readRoot)
                && IsCappedBufferLoad(candidate, model, httpContent, cancellationToken)
                && Dominates(candidate, read, scope))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsCappedBufferLoad(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        INamedTypeSymbol httpContent,
        CancellationToken cancellationToken)
    {
        return model.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol method
               && string.Equals(method.Name, CappedBufferLoadName, StringComparison.Ordinal)
               && TypeSymbols.DerivesFrom(method.ContainingType, httpContent)
               && method.Parameters.Length is 1
               && method.Parameters[0].Type.SpecialType is SpecialType.System_Int64;
    }

    /// <summary>
    ///     True when a <c lang="csharp">ContentLength</c> check on the same content chain bounds
    ///     the read. The read inside the then branch needs a condition that is false when the
    ///     length is null. The read outside it needs a condition that is true when the length is
    ///     null, and a then branch that stops the flow, so a chunked response cannot pass through.
    /// </summary>
    private static bool HasContentLengthBound(
        SyntaxNode scope,
        InvocationExpressionSyntax read,
        string readRoot,
        SemanticModel model,
        IPropertySymbol? contentLength,
        HashSet<ISymbol> boundVariables,
        CancellationToken cancellationToken)
    {
        foreach (var check in scope.DescendantNodes().OfType<IfStatementSyntax>())
        {
            if (InvolvesBound(check.Condition, model, contentLength, boundVariables, readRoot, cancellationToken)
                && IsNullSafeBound(check, read, scope))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     True when a <c lang="csharp">ContentLength</c> check bounds the read. The read inside
    ///     the then branch needs a condition that is false when the length is null. The read
    ///     outside it needs a condition that is true when the length is null, and a then branch
    ///     that stops the flow, so a chunked response cannot pass through.
    /// </summary>
    private static bool IsNullSafeBound(IfStatementSyntax check, InvocationExpressionSyntax read, SyntaxNode scope)
    {
        if (!Dominates(check, read, scope))
        {
            return false;
        }

        var readInThen = check.Statement.Span.Contains(read.Span);
        var holdsWhenNull = TrueWhenNull(check.Condition);

        return readInThen
            ? holdsWhenNull is false
            : holdsWhenNull is true && Terminates(check.Statement);
    }

    /// <summary>
    ///     True when a bound node precedes the read, or encloses it, and no conditional body
    ///     between the bound and the method scope excludes the read. A bound inside a branch the
    ///     read can skip does not bound the read. A lambda or local function boundary between them
    ///     hides when the bound runs, so it rejects too.
    /// </summary>
    private static bool Dominates(SyntaxNode bound, InvocationExpressionSyntax read, SyntaxNode scope)
    {
        if (!bound.Span.Contains(read.Span) && bound.Span.End > read.SpanStart)
        {
            return false;
        }

        for (var node = bound.Parent; node is not null && node != scope; node = node.Parent)
        {
            if (node is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax)
            {
                return false;
            }

            if (node is StatementSyntax statement
                && !statement.Span.Contains(read.Span)
                && IsBranchBody(node))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsBranchBody(SyntaxNode node)
    {
        return node.Parent switch
        {
            IfStatementSyntax ifStatement => ifStatement.Statement == node || ifStatement.Else == node,
            ElseClauseSyntax elseClause => elseClause.Statement == node,
            SwitchSectionSyntax section => SectionContains(section, node),
            CatchClauseSyntax catchClause => catchClause.Block == node,
            FinallyClauseSyntax finallyClause => finallyClause.Block == node,
            _ => IsLoopBody(node)
        };
    }

    private static bool IsLoopBody(SyntaxNode node)
    {
        return node.Parent switch
        {
            ForStatementSyntax forStatement => forStatement.Statement == node,
            CommonForEachStatementSyntax forEachStatement => forEachStatement.Statement == node,
            WhileStatementSyntax whileStatement => whileStatement.Statement == node,
            DoStatementSyntax doStatement => doStatement.Statement == node,
            _ => false
        };
    }

    private static bool SectionContains(SwitchSectionSyntax section, SyntaxNode node)
    {
        foreach (var statement in section.Statements)
        {
            if (ReferenceEquals(statement, node))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Terminates(SyntaxNode node)
    {
        foreach (var child in node.ChildNodes())
        {
            if (child is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax)
            {
                continue;
            }

            if (child.Kind() is SyntaxKind.ThrowStatement or SyntaxKind.ThrowExpression
                or SyntaxKind.ReturnStatement or SyntaxKind.BreakStatement
                or SyntaxKind.ContinueStatement)
            {
                return true;
            }

            if (Terminates(child))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     True when the condition involves a resolved <c lang="csharp">ContentLength</c> property
    ///     on the same content chain as the read, or a local that a <c lang="csharp">ContentLength</c>
    ///     expression initialized.
    /// </summary>
    private static bool InvolvesBound(
        ExpressionSyntax condition,
        SemanticModel model,
        IPropertySymbol? contentLength,
        HashSet<ISymbol> boundVariables,
        string readRoot,
        CancellationToken cancellationToken)
    {
        foreach (var node in condition.DescendantNodesAndSelf())
        {
            if (node is MemberAccessExpressionSyntax access
                && SameRoot(access, readRoot)
                && IsContentLengthProperty(access, model, contentLength, cancellationToken))
            {
                return true;
            }

            if (node is IdentifierNameSyntax identifier
                && model.GetSymbolInfo(identifier, cancellationToken).Symbol is ILocalSymbol local
                && boundVariables.Contains(local))
            {
                return true;
            }
        }

        return false;
    }

    private static HashSet<ISymbol> CollectBoundVariables(
        SyntaxNode scope,
        SemanticModel model,
        IPropertySymbol? contentLength,
        string readRoot,
        CancellationToken cancellationToken)
    {
        var boundVariables = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

        foreach (var declarator in scope.DescendantNodes().OfType<VariableDeclaratorSyntax>())
        {
            var local = BoundLocal(declarator, model, contentLength, readRoot, cancellationToken);
            if (local is not null)
            {
                boundVariables.Add(local);
            }
        }

        return boundVariables;
    }

    private static ILocalSymbol? BoundLocal(
        VariableDeclaratorSyntax declarator,
        SemanticModel model,
        IPropertySymbol? contentLength,
        string readRoot,
        CancellationToken cancellationToken)
    {
        if (Unwrap(declarator.Initializer?.Value) is not MemberAccessExpressionSyntax access
            || !SameRoot(access, readRoot)
            || !IsContentLengthProperty(access, model, contentLength, cancellationToken)
            || model.GetDeclaredSymbol(declarator, cancellationToken) is not ILocalSymbol local)
        {
            return null;
        }

        return local;
    }

    private static bool IsContentLengthProperty(
        MemberAccessExpressionSyntax access,
        SemanticModel model,
        IPropertySymbol? contentLength,
        CancellationToken cancellationToken)
    {
        return contentLength is not null
               && model.GetSymbolInfo(access, cancellationToken).Symbol is IPropertySymbol property
               && SymbolEqualityComparer.Default.Equals(property, contentLength);
    }

    private static IPropertySymbol? GetContentLengthProperty(INamedTypeSymbol? httpContentHeaders)
    {
        return httpContentHeaders?.GetMembers()
            .OfType<IPropertySymbol>()
            .FirstOrDefault(static property
                => string.Equals(property.Name, ContentLengthName, StringComparison.Ordinal)
                   && property.Parameters.Length is 0);
    }

    /// <summary>
    ///     Three-valued evaluation of a condition when the nullable length operand is null. Null
    ///     means the outcome depends on another operand. A lifted comparison is false when null,
    ///     so a plain greater-than guard does not hold and cannot bound the read.
    /// </summary>
    private static bool? TrueWhenNull(ExpressionSyntax expression)
    {
        if (expression.IsKind(SyntaxKind.ParenthesizedExpression))
        {
            return TrueWhenNull(((ParenthesizedExpressionSyntax)expression).Expression);
        }

        if (expression.IsKind(SyntaxKind.LogicalNotExpression))
        {
            return Not(TrueWhenNull(((PrefixUnaryExpressionSyntax)expression).Operand));
        }

        if (expression.IsKind(SyntaxKind.LogicalAndExpression))
        {
            var andExpression = (BinaryExpressionSyntax)expression;
            return And(TrueWhenNull(andExpression.Left), TrueWhenNull(andExpression.Right));
        }

        if (expression.IsKind(SyntaxKind.LogicalOrExpression))
        {
            var orExpression = (BinaryExpressionSyntax)expression;
            return Or(TrueWhenNull(orExpression.Left), TrueWhenNull(orExpression.Right));
        }

        if (expression.IsKind(SyntaxKind.IsPatternExpression))
        {
            return PatternHoldsWhenNull(((IsPatternExpressionSyntax)expression).Pattern);
        }

        return ComparisonHoldsWhenNull(expression);
    }

    private static bool? ComparisonHoldsWhenNull(ExpressionSyntax expression)
    {
        if (expression.IsKind(SyntaxKind.EqualsExpression))
        {
            return ComparesNull((BinaryExpressionSyntax)expression);
        }

        if (expression.IsKind(SyntaxKind.NotEqualsExpression))
        {
            return Not(ComparesNull((BinaryExpressionSyntax)expression));
        }

        if (expression.Kind()
            is SyntaxKind.GreaterThanExpression
            or SyntaxKind.GreaterThanOrEqualExpression
            or SyntaxKind.LessThanExpression
            or SyntaxKind.LessThanOrEqualExpression)
        {
            return false;
        }

        return null;
    }

    private static bool ComparesNull(BinaryExpressionSyntax comparison)
    {
        return comparison.Left.IsKind(SyntaxKind.NullLiteralExpression)
               || comparison.Right.IsKind(SyntaxKind.NullLiteralExpression);
    }

    private static bool? PatternHoldsWhenNull(PatternSyntax pattern)
    {
        if (pattern is RelationalPatternSyntax)
        {
            return false;
        }

        if (pattern is ConstantPatternSyntax { Expression: { } constantExpression })
        {
            return constantExpression.IsKind(SyntaxKind.NullLiteralExpression);
        }

        if (pattern is UnaryPatternSyntax negated)
        {
            return Not(PatternHoldsWhenNull(negated.Pattern));
        }

        if (pattern is ParenthesizedPatternSyntax parenthesized)
        {
            return PatternHoldsWhenNull(parenthesized.Pattern);
        }

        return BinaryPatternHoldsWhenNull(pattern);
    }

    private static bool? BinaryPatternHoldsWhenNull(PatternSyntax pattern)
    {
        if (pattern is not BinaryPatternSyntax binary)
        {
            return null;
        }

        return binary.IsKind(SyntaxKind.OrPattern)
            ? Or(PatternHoldsWhenNull(binary.Left), PatternHoldsWhenNull(binary.Right))
            : And(PatternHoldsWhenNull(binary.Left), PatternHoldsWhenNull(binary.Right));
    }

    private static bool? And(bool? left, bool? right)
    {
        return left is false || right is false
            ? false
            : left is true && right is true
                ? true
                : null;
    }

    private static bool? Or(bool? left, bool? right)
    {
        return left is true || right is true
            ? true
            : left is false && right is false
                ? false
                : null;
    }

    private static bool? Not(bool? value)
    {
        return value switch
        {
            true => false,
            false => true,
            _ => null
        };
    }

    /// <summary>
    ///     Returns true when the leftmost receiver of an expression matches the read root, so a
    ///     bound and a read are tied to the same content chain.
    /// </summary>
    private static bool SameRoot(ExpressionSyntax expression, string readRoot)
    {
        return string.Equals(RootName(expression), readRoot, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Returns the identifier text of the leftmost receiver of an expression, so a bound and a
    ///     read can be tied to the same content chain. An expression without a plain receiver root,
    ///     such as a method call chain, returns the empty string and matches nothing.
    /// </summary>
    private static string RootName(ExpressionSyntax? expression)
    {
        var current = expression;
        while (true)
        {
            switch (current)
            {
                case MemberAccessExpressionSyntax member:
                    current = member.Expression;
                    continue;

                case ConditionalAccessExpressionSyntax conditional:
                    current = conditional.Expression;
                    continue;

                case ParenthesizedExpressionSyntax parenthesized:
                    current = parenthesized.Expression;
                    continue;

                case PostfixUnaryExpressionSyntax postfix when postfix.IsKind(SyntaxKind.SuppressNullableWarningExpression):
                    current = postfix.Operand;
                    continue;

                case IdentifierNameSyntax identifier:
                    return identifier.Identifier.ValueText;

                case ThisExpressionSyntax:
                    return "this";

                default:
                    return string.Empty;
            }
        }
    }

    private static ExpressionSyntax? Unwrap(ExpressionSyntax? expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression;
    }

    private static bool Mentions(SyntaxNode scope, ImmutableHashSet<string> names)
    {
        return scope.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Any(name => names.Contains(name.Identifier.ValueText));
    }
}

using System;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Security;

/// <summary>
///     E128157: Reports a byte comparison of a secret-derived value that runs in variable time.
///     A <c lang="csharp">==</c>, <c lang="csharp">!=</c>, or
///     <c lang="csharp">SequenceEqual</c> over an authentication tag or a signature leaks the
///     matching prefix length through its running time.
/// </summary>
/// <remarks>
///     The rule reads a value produced by an HMAC member, or a local or field whose name marks it
///     as a tag, MAC, or signature. A plain content hash stays silent, because a hash compare is not
///     a forgery check.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TimingSafeComparisonAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128157";

    private static readonly ImmutableHashSet<string> SecretNameSuffixes = ImmutableHashSet.Create(
        StringComparer.OrdinalIgnoreCase,
        "Mac",
        "Hmac",
        "Tag",
        "Signature");

    private static readonly ImmutableHashSet<string> SecretNameSegments = ImmutableHashSet.Create(
        StringComparer.OrdinalIgnoreCase,
        "mac",
        "hmac",
        "tag",
        "signature");

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Fixed-time comparison of a secret-derived value",
        "Compare '{0}' in fixed time",
        "Security",
        DiagnosticSeverity.Warning,
        true,
        "An equality test on an authentication tag or a signature returns as soon as two bytes " +
        "differ. The running time then leaks the length of the matching prefix, which allows an " +
        "attacker to forge a valid value one byte at a time. Use " +
        "CryptographicOperations.FixedTimeEquals.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeBinary, SyntaxKind.EqualsExpression);
        context.RegisterSyntaxNodeAction(AnalyzeBinary, SyntaxKind.NotEqualsExpression);
        context.RegisterSyntaxNodeAction(AnalyzeSequenceEqual, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeBinary(SyntaxNodeAnalysisContext context)
    {
        var binary = (BinaryExpressionSyntax)context.Node;
        var model = context.SemanticModel;

        // An equality operator on a byte array compares references, not content, so it cannot leak a
        // matching prefix through its running time. The span forms are the shapes this rule reads.
        if (!IsByteSpan(binary.Left, model, context.CancellationToken)
            || !IsByteSpan(binary.Right, model, context.CancellationToken))
        {
            return;
        }

        if (!IsSecretDerived(binary.Left, model, context.CancellationToken)
            && !IsSecretDerived(binary.Right, model, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, binary.GetLocation(), binary.ToString()));
    }

    private static void AnalyzeSequenceEqual(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess
            || !string.Equals(memberAccess.Name.Identifier.ValueText, "SequenceEqual", StringComparison.Ordinal))
        {
            return;
        }

        var model = context.SemanticModel;
        var receiver = memberAccess.Expression;

        if (!IsByteSequence(receiver, model, context.CancellationToken)
            || invocation.ArgumentList.Arguments.Count != 1
            || !IsByteSequence(invocation.ArgumentList.Arguments[0].Expression, model, context.CancellationToken))
        {
            return;
        }

        if (!IsSecretDerived(receiver, model, context.CancellationToken)
            && !IsSecretDerived(invocation.ArgumentList.Arguments[0].Expression, model, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), invocation.ToString()));
    }

    /// <summary>
    ///     Returns true when the expression holds a byte array or a byte span, which is the value
    ///     <c lang="csharp">SequenceEqual</c> compares by content.
    /// </summary>
    private static bool IsByteSequence(
        ExpressionSyntax expression,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        return IsByteSpan(expression, model, cancellationToken)
               || model.GetTypeInfo(expression, cancellationToken).Type
                   is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_Byte };
    }

    /// <summary>
    ///     Returns true when the expression holds a <c lang="csharp">Span&lt;byte&gt;</c> or
    ///     <c lang="csharp">ReadOnlySpan&lt;byte&gt;</c>.
    /// </summary>
    private static bool IsByteSpan(
        ExpressionSyntax expression,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        return model.GetTypeInfo(expression, cancellationToken).Type
               is INamedTypeSymbol { TypeArguments.Length: 1 } namedType
               && namedType.TypeArguments[0].SpecialType == SpecialType.System_Byte
               && namedType.Name is "ReadOnlySpan" or "Span";
    }

    private static bool IsSecretDerived(
        ExpressionSyntax expression,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        var value = Unwrap(expression);
        var symbol = model.GetSymbolInfo(value, cancellationToken).Symbol;

        return (symbol is not null && HasSecretName(symbol.Name))
               || IsProducedBySecretApi(value, symbol, model, cancellationToken);
    }

    private static bool IsProducedBySecretApi(
        ExpressionSyntax value,
        ISymbol? symbol,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        return value is InvocationExpressionSyntax invocation
            ? IsSecretProducer(invocation, model, cancellationToken)
            : symbol switch
            {
                ILocalSymbol local => InitializerProducesSecret(local, model, cancellationToken),
                IFieldSymbol field => InitializerProducesSecret(field, model, cancellationToken),
                _ => false
            };
    }

    /// <summary>
    ///     Removes the wrappers that do not change the value, so a parenthesized expression and a
    ///     span view of it resolve to the same producer.
    /// </summary>
    private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
    {
        var current = expression;

        while (UnwrapOnce(current) is { } inner)
        {
            current = inner;
        }

        return current;
    }

    private static ExpressionSyntax? UnwrapOnce(ExpressionSyntax expression)
    {
        return expression switch
        {
            ParenthesizedExpressionSyntax parenthesized => parenthesized.Expression,
            InvocationExpressionSyntax
            {
                ArgumentList.Arguments.Count: 0,
                Expression: MemberAccessExpressionSyntax
                {
                    Name.Identifier.ValueText: "AsSpan" or "AsMemory"
                } access
            } => access.Expression,
            _ => null
        };
    }

    private static bool HasSecretName(string name)
    {
        foreach (var suffix in SecretNameSuffixes)
        {
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        foreach (var segment in name.Split('_', '.'))
        {
            if (SecretNameSegments.Contains(segment))
            {
                return true;
            }
        }

        return false;
    }

    private static bool InitializerProducesSecret(
        ISymbol symbol,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        foreach (var reference in symbol.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax(cancellationToken) is not VariableDeclaratorSyntax
                {
                    Initializer.Value: { } initializer
                })
            {
                continue;
            }

            if (Unwrap(initializer) is InvocationExpressionSyntax invocation
                && IsSecretProducer(invocation, model, cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSecretProducer(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        if (model.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol method)
        {
            return false;
        }

        var typeName = method.ContainingType?.Name;

        return typeName is not null
               && typeName.StartsWith("HMAC", StringComparison.Ordinal);
    }
}


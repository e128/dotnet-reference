using System;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Security;

/// <summary>
///     E128157: Reports a comparison of a secret-derived value that runs in variable time.
///     <c lang="csharp">SequenceEqual</c> over a byte array or a span, and a string
///     <c lang="csharp">==</c>, <c lang="csharp">!=</c>, or <c lang="csharp">Equals</c> over an
///     authentication tag or a signature, leak the matching prefix length through their running time.
/// </summary>
/// <remarks>
///     The rule reads a value produced by an HMAC member, or a local or field whose name marks it
///     as a tag, MAC, or signature. A plain content hash stays silent, because a hash compare is not
///     a forgery check. The equality operator on a span compares the address and the length in
///     constant time, so the rule does not read that shape. A field initializer in another syntax
///     tree is a known false negative. RS1030 forbids <c lang="csharp">Compilation.GetSemanticModel</c> inside an
///     analyzer, so only a declarator in the current tree can be inspected.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TimingSafeComparisonAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128157";

    private const int MaxDepth = 3;

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

    // A weak word marks a string as a possible secret only when both operands carry a weak word.
    private static readonly ImmutableHashSet<string> WeakNameWords = ImmutableHashSet.Create(
        StringComparer.OrdinalIgnoreCase,
        "token",
        "digest",
        "expected",
        "computed",
        "hash");

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Fixed-time comparison of a secret-derived value",
        "Compare '{0}' in fixed time",
        "E128-Security",
        DiagnosticSeverity.Warning,
        true,
        "An equality test on an authentication tag or a signature returns as soon as two values " +
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
        context.RegisterSyntaxNodeAction(AnalyzeStringOperator, SyntaxKind.EqualsExpression);
        context.RegisterSyntaxNodeAction(AnalyzeStringOperator, SyntaxKind.NotEqualsExpression);
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeStringOperator(SyntaxNodeAnalysisContext context)
    {
        var binary = (BinaryExpressionSyntax)context.Node;

        // The equality operator on a span compares the address and the length, never the content,
        // and it takes constant time. Only the string forms leak a matching prefix.
        if (IsSecretStringComparison(binary.Left, binary.Right, context.SemanticModel, context.CancellationToken))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, binary.GetLocation(), binary.ToString()));
        }
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var model = context.SemanticModel;
        var cancellationToken = context.CancellationToken;

        var reported = GetInvokedName(invocation) switch
        {
            "SequenceEqual" => IsSecretSequenceEqual(invocation, model, cancellationToken),
            "Equals" => IsSecretStringEquals(invocation, model, cancellationToken),
            _ => false
        };

        if (reported)
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), invocation.ToString()));
        }
    }

    private static string? GetInvokedName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            _ => null
        };
    }

    private static bool IsSecretSequenceEqual(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        return TryGetSequenceEqualOperands(invocation, model, cancellationToken, out var left, out var right)
               && IsByteSequence(left, model, cancellationToken)
               && IsByteSequence(right, model, cancellationToken)
               && (IsSecretDerived(left, model, depth: 0, cancellationToken)
                   || IsSecretDerived(right, model, depth: 0, cancellationToken));
    }

    /// <summary>
    ///     Reads the two operands of the extension form <c lang="csharp">a.SequenceEqual(b)</c> and of
    ///     the static form <c lang="csharp">MemoryExtensions.SequenceEqual(a, b)</c>.
    /// </summary>
    private static bool TryGetSequenceEqualOperands(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        CancellationToken cancellationToken,
        out ExpressionSyntax left,
        out ExpressionSyntax right)
    {
        var arguments = invocation.ArgumentList.Arguments;
        left = invocation;
        right = invocation;

        if (arguments.Count == 1 && invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            left = memberAccess.Expression;
            right = arguments[0].Expression;

            return true;
        }

        if (arguments.Count == 2
            && model.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol { IsStatic: true } method
            && method.ContainingType.ToDisplayString() is "System.Linq.Enumerable" or "System.MemoryExtensions")
        {
            left = arguments[0].Expression;
            right = arguments[1].Expression;

            return true;
        }

        return false;
    }

    private static bool IsSecretStringEquals(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        if (model.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol method
            || method.ContainingType.SpecialType is not (SpecialType.System_String or SpecialType.System_Object))
        {
            return false;
        }

        var arguments = invocation.ArgumentList.Arguments;

        return method.IsStatic
            ? arguments.Count >= 2
              && IsSecretStringComparison(
                  arguments[0].Expression, arguments[1].Expression, model, cancellationToken)
            : arguments.Count >= 1
              && invocation.Expression is MemberAccessExpressionSyntax memberAccess
              && IsSecretStringComparison(
                  memberAccess.Expression, arguments[0].Expression, model, cancellationToken);
    }

    /// <summary>
    ///     Returns true when both operands are strings, neither is a constant, and at least one
    ///     secret signal exists. A comparison with a constant is a lookup, not a forgery check.
    /// </summary>
    private static bool IsSecretStringComparison(
        ExpressionSyntax left,
        ExpressionSyntax right,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        if (!IsString(left, model, cancellationToken)
            || !IsString(right, model, cancellationToken)
            || model.GetConstantValue(left, cancellationToken).HasValue
            || model.GetConstantValue(right, cancellationToken).HasValue)
        {
            return false;
        }

        if (IsSecretDerived(left, model, depth: 0, cancellationToken)
            || IsSecretDerived(right, model, depth: 0, cancellationToken))
        {
            return true;
        }

        var leftWeak = GetWeakWord(left, model, cancellationToken);
        var rightWeak = GetWeakWord(right, model, cancellationToken);

        // Two weak names, such as token and expectedToken, form a secret signal. Two plain hash
        // names do not, because a content hash compare is not a forgery check.
        return leftWeak is not null
               && rightWeak is not null
               && (!string.Equals(leftWeak, "hash", StringComparison.OrdinalIgnoreCase)
                   || !string.Equals(rightWeak, "hash", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsString(
        ExpressionSyntax expression,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        return model.GetTypeInfo(expression, cancellationToken).Type?.SpecialType == SpecialType.System_String;
    }

    private static string? GetWeakWord(
        ExpressionSyntax expression,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        var symbol = model.GetSymbolInfo(Unwrap(expression), cancellationToken).Symbol;

        if (symbol is null)
        {
            return null;
        }

        string? hashOnly = null;

        foreach (var word in WordSplitter.Split(symbol.Name, "_."))
        {
            if (!WeakNameWords.Contains(word))
            {
                continue;
            }

            if (!string.Equals(word, "hash", StringComparison.OrdinalIgnoreCase))
            {
                return word;
            }

            hashOnly = word;
        }

        return hashOnly;
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
        int depth,
        CancellationToken cancellationToken)
    {
        if (depth > MaxDepth)
        {
            return false;
        }

        var value = Unwrap(expression);
        var symbol = model.GetSymbolInfo(value, cancellationToken).Symbol;

        return (symbol is not null && HasSecretName(symbol.Name))
               || IsProducedBySecretApi(value, symbol, model, depth, cancellationToken);
    }

    private static bool IsProducedBySecretApi(
        ExpressionSyntax value,
        ISymbol? symbol,
        SemanticModel model,
        int depth,
        CancellationToken cancellationToken)
    {
        return ProducesSecret(value, model, depth, cancellationToken)
               || symbol switch
               {
                   ILocalSymbol local => InitializerProducesSecret(local, model, depth, cancellationToken),
                   IFieldSymbol field => InitializerProducesSecret(field, model, depth, cancellationToken),
                   _ => false
               };
    }

    /// <summary>
    ///     Returns true when the expression calls an HMAC member, reads the
    ///     <c lang="csharp">Hash</c> property of an HMAC, or encodes such a value as hex or base64.
    /// </summary>
    private static bool ProducesSecret(
        ExpressionSyntax expression,
        SemanticModel model,
        int depth,
        CancellationToken cancellationToken)
    {
        return Unwrap(expression) switch
        {
            InvocationExpressionSyntax invocation =>
                IsSecretProducer(invocation, model, cancellationToken)
                || IsEncodedSecret(invocation, model, depth, cancellationToken),
            MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Hash" } access =>
                model.GetSymbolInfo(access, cancellationToken).Symbol is IPropertySymbol
                && IsHmacType(model.GetTypeInfo(access.Expression, cancellationToken).Type),
            _ => false
        };
    }

    /// <summary>
    ///     Removes the wrappers that do not change the value, so a parenthesized expression, a span
    ///     view, and a case change of it resolve to the same producer.
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
                        or "ToLowerInvariant" or "ToUpperInvariant"
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
        int depth,
        CancellationToken cancellationToken)
    {
        foreach (var reference in symbol.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax(cancellationToken) is not VariableDeclaratorSyntax
                {
                    Initializer.Value: { } initializer
                } declarator)
            {
                continue;
            }

            // Only a declarator in the current syntax tree is inspected. RS1030 forbids
            // Compilation.GetSemanticModel inside an analyzer, so a cross-tree initializer is a
            // known false negative: a field initialized in another partial file contributes no
            // secret signal.
            var declarationModel = declarator.SyntaxTree == model.SyntaxTree
                ? model
                : null;

            if (declarationModel is not null
                && ProducesSecret(initializer, declarationModel, depth + 1, cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsEncodedSecret(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        int depth,
        CancellationToken cancellationToken)
    {
        if (invocation.ArgumentList.Arguments.Count == 0
            || model.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol method)
        {
            return false;
        }

        var isEncoder = method.ContainingType.ToDisplayString() switch
        {
            "System.Convert" => method.Name is "ToHexString" or "ToHexStringLower" or "ToBase64String",
            "System.Buffers.Text.Base64Url" => method.Name is "EncodeToString",
            _ => false
        };

        return isEncoder
               && IsSecretDerived(
                   invocation.ArgumentList.Arguments[0].Expression, model, depth + 1, cancellationToken);
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

        // HMAC.ComputeHash is declared on HashAlgorithm, so the declaring type alone misses it.
        // Read the receiver type and walk its base types to the HMAC type.
        return IsHmacType(method.ContainingType)
               || (invocation.Expression is MemberAccessExpressionSyntax access
                   && (IsHmacType(model.GetTypeInfo(access.Expression, cancellationToken).Type)
                       || (string.Equals(method.ContainingType.Name, "IncrementalHash", StringComparison.Ordinal)
                           && method.Name is "GetCurrentHash" or "GetHashAndReset" or "TryGetCurrentHash"
                               or "TryGetHashAndReset"
                           && IsHmacIncrementalHash(access.Expression, model, cancellationToken))));
    }

    private static bool IsHmacType(ITypeSymbol? type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.Name.StartsWith("HMAC", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Returns true when the expression is, or is initialized from, a call to
    ///     <c lang="csharp">IncrementalHash.CreateHMAC</c>.
    /// </summary>
    private static bool IsHmacIncrementalHash(
        ExpressionSyntax expression,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        var value = Unwrap(expression);

        if (IsCreateHmacCall(value, model, cancellationToken))
        {
            return true;
        }

        var symbol = model.GetSymbolInfo(value, cancellationToken).Symbol;

        if (symbol is not (ILocalSymbol or IFieldSymbol))
        {
            return false;
        }

        foreach (var reference in symbol.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax(cancellationToken) is VariableDeclaratorSyntax
                {
                    Initializer.Value: { } initializer
                } declarator
                && declarator.SyntaxTree == model.SyntaxTree
                && IsCreateHmacCall(Unwrap(initializer), model, cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsCreateHmacCall(
        ExpressionSyntax value,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        return value is InvocationExpressionSyntax invocation
               && model.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol
               {
                   Name: "CreateHMAC",
                   ContainingType.Name: "IncrementalHash"
               };
    }
}

using System;
using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace E128.Analyzers.Security;

/// <summary>
///     E128146: Reports TLS configuration that allows protocols below 1.2: the SSL and TLS 1.0 and 1.1 members
///     of <c lang="csharp">SslProtocols</c> and <c lang="csharp">SecurityProtocolType</c>, and the
///     <c lang="csharp">DontEnableSchUseStrongCrypto</c> and <c lang="csharp">DontEnableSystemDefaultTlsVersions</c>
///     AppContext switches.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FipsTlsVersionAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128146";

    private const string StrongCryptoSwitch = "Switch.System.Net.DontEnableSchUseStrongCrypto";

    private const string SystemDefaultSwitch = "Switch.System.Net.DontEnableSystemDefaultTlsVersions";

    // Bits of SslProtocols and SecurityProtocolType below TLS 1.2: Ssl2 (12), Ssl3 (48), Tls (192) and Tls11 (768).
    private const int WeakProtocolBits = 12 | 48 | 192 | 768;

    // Members that enable a protocol below TLS 1.2. Tls12, Tls13, None and SystemDefault are allowed.
    private static readonly ImmutableHashSet<string> ForbiddenMembers = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "Ssl2",
        "Ssl3",
        "Tls",
        "Tls11",
        "Default");

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Use TLS 1.2 or later",
        "{0}",
        "E128-Security",
        DiagnosticSeverity.Error,
        true,
        "SSL 2.0, SSL 3.0, TLS 1.0 and TLS 1.1 are not permitted. Use TLS 1.2 or 1.3, or SslProtocols.None " +
        "to defer to the operating system configuration. The rule skips comparisons and mask removal.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(
            GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();

        context.RegisterOperationAction(AnalyzeFieldReference, OperationKind.FieldReference);
        context.RegisterOperationAction(AnalyzeNumericCast, OperationKind.Conversion);
        context.RegisterOperationAction(AnalyzeNegation, OperationKind.Unary);
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
    }

    private static void AnalyzeFieldReference(OperationAnalysisContext context)
    {
        var fieldReference = (IFieldReferenceOperation)context.Operation;
        var field = fieldReference.Field;

        if (!ForbiddenMembers.Contains(field.Name))
        {
            return;
        }

        if (IsProtocolType(field.ContainingType) && !IsRead(fieldReference))
        {
            Report(
                context,
                fieldReference,
                $"'{field.Name}' enables a TLS/SSL protocol below 1.2, which is not permitted; use Tls12 or Tls13");
        }
    }

    /// <summary>
    ///     Returns true when the protocol value is read, not selected: a comparison, a bit test, or a mask that
    ///     removes the protocol.
    /// </summary>
    private static bool IsRead(IOperation operand) =>
        FipsSymbolHelpers.IsComparison(operand) || IsMaskTest(operand) || IsMaskRemoval(operand);

    /// <summary>
    ///     Returns true for <c lang="csharp">x &amp; SslProtocols.Tls11</c> when the result feeds a comparison, an
    ///     <c lang="csharp">is</c> pattern or <c lang="csharp">HasFlag</c>, such as
    ///     <c lang="csharp">(x &amp; SslProtocols.Tls11) != 0</c>. The mask reads a bit and enables nothing.
    /// </summary>
    private static bool IsMaskTest(IOperation operand)
    {
        var parent = operand.Parent;
        while (parent is IConversionOperation)
        {
            parent = parent.Parent;
        }

        if (parent is not IBinaryOperation { OperatorKind: BinaryOperatorKind.And } mask)
        {
            return false;
        }

        var outer = mask.Parent;
        while (outer is IConversionOperation)
        {
            outer = outer.Parent;
        }

        return outer is IIsPatternOperation || FipsSymbolHelpers.IsComparison(mask);
    }

    /// <summary>
    ///     Returns true for <c lang="csharp">~SslProtocols.Tls11</c> or <c lang="csharp">~(SslProtocols.Tls | SslProtocols.Tls11)</c> used as an
    ///     <c lang="csharp">&amp;</c> or <c lang="csharp">&amp;=</c> operand, which removes the protocols from a mask instead of enabling them.
    /// </summary>
    private static bool IsMaskRemoval(IOperation operand)
    {
        var parent = operand.Parent;
        while (parent is IConversionOperation or IBinaryOperation { OperatorKind: BinaryOperatorKind.Or })
        {
            parent = parent.Parent;
        }

        if (parent is not IUnaryOperation { OperatorKind: UnaryOperatorKind.BitwiseNegation } negation)
        {
            return false;
        }

        var outer = negation.Parent;
        while (outer is IConversionOperation)
        {
            outer = outer.Parent;
        }

        return outer is IBinaryOperation { OperatorKind: BinaryOperatorKind.And } or ICompoundAssignmentOperation
        {
            OperatorKind: BinaryOperatorKind.And,
        };
    }

    private static bool IsProtocolType(ITypeSymbol? type) =>
        FipsSymbolHelpers.IsType(type, "SslProtocols", "System.Security.Authentication") ||
        FipsSymbolHelpers.IsType(type, "SecurityProtocolType", "System.Net");

    private static bool HasWeakBits(IOperation operation) =>
        operation.ConstantValue is { HasValue: true, Value: { } value } &&
        Convert.ToInt64(value, CultureInfo.InvariantCulture) is var bits &&
        (bits & WeakProtocolBits) != 0;

    /// <summary>Reports a numeric cast such as <c lang="csharp">(SslProtocols)192</c> that includes a weak protocol bit.</summary>
    private static void AnalyzeNumericCast(OperationAnalysisContext context)
    {
        var conversion = (IConversionOperation)context.Operation;
        if (conversion.IsImplicit ||
            !IsProtocolType(conversion.Type) ||
            conversion.Operand.Type?.TypeKind == TypeKind.Enum ||
            !HasWeakBits(conversion) ||
            IsRead(conversion))
        {
            return;
        }

        Report(
            context,
            conversion,
            "A numeric value enables a TLS/SSL protocol below 1.2, which is not permitted; use Tls12 or Tls13");
    }

    /// <summary>
    ///     Reports <c lang="csharp">~SslProtocols.None</c> and similar complements of an allowed member, which set every weak
    ///     protocol bit. A complement used as an <c lang="csharp">&amp;</c> mask is not reported.
    /// </summary>
    private static void AnalyzeNegation(OperationAnalysisContext context)
    {
        var negation = (IUnaryOperation)context.Operation;
        if (negation.OperatorKind != UnaryOperatorKind.BitwiseNegation ||
            !IsProtocolType(negation.Type) ||
            FipsSymbolHelpers.Unwrap(negation.Operand) is not IFieldReferenceOperation operand ||
            ForbiddenMembers.Contains(operand.Field.Name) ||
            !HasWeakBits(negation) ||
            IsMaskRemoval(operand))
        {
            return;
        }

        Report(
            context,
            negation,
            "The complement of a protocol enables every protocol below 1.2, which is not permitted; use Tls12 or Tls13");
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;
        if (!string.Equals(method.Name, "SetSwitch", StringComparison.Ordinal) ||
            !FipsSymbolHelpers.IsType(method.ContainingType, "AppContext", "System"))
        {
            return;
        }

        // Resolve by parameter, because named arguments can reorder the call.
        var switchName = FipsSymbolHelpers.ArgumentAt(invocation.Arguments, 0);
        var isEnabled = FipsSymbolHelpers.ArgumentAt(invocation.Arguments, 1);
        if (switchName is null || isEnabled is null)
        {
            return;
        }

        if (!FipsSymbolHelpers.TryGetResolvedConstant<string>(switchName.Value, out var name) ||
            !FipsSymbolHelpers.TryGetResolvedConstant<bool>(isEnabled.Value, out var enabled) ||
            !enabled)
        {
            return;
        }

        string? message = null;
        if (string.Equals(name, StrongCryptoSwitch, StringComparison.Ordinal))
        {
            message = "Setting 'DontEnableSchUseStrongCrypto' to true disables strong crypto and re-enables weak TLS; remove this switch";
        }
        else if (string.Equals(name, SystemDefaultSwitch, StringComparison.Ordinal))
        {
            message = "Setting 'DontEnableSystemDefaultTlsVersions' to true stops the operating system from choosing the protocol and re-enables TLS 1.0 and 1.1; remove this switch";
        }

        if (message is not null)
        {
            Report(context, invocation, message);
        }
    }

    private static void Report(OperationAnalysisContext context, IOperation operation, string message) =>
        context.ReportDiagnostic(Diagnostic.Create(Rule, operation.Syntax.GetLocation(), message));
}

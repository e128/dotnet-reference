using System;
using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace E128.Analyzers.Security;

/// <summary>
///     E128144: Reports the ECB, OFB and CFB cipher modes. The rule fires on a reference to a
///     <c lang="csharp">CipherMode</c> member, on the mode-specific <c lang="csharp">SymmetricAlgorithm</c> methods,
///     and on a constant numeric cast such as <c lang="csharp">(CipherMode)2</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FipsCipherModeAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128144";

    private static readonly ImmutableHashSet<string> ForbiddenModes = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "ECB",
        "OFB",
        "CFB");

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Use an authenticated cipher mode",
        "Cipher mode '{0}' is not permitted; use authenticated encryption such as AES-GCM",
        "E128-Security",
        DiagnosticSeverity.Error,
        true,
        "ECB reveals plaintext structure. The OFB and CFB modes are not permitted by policy. Use " +
        "authenticated encryption such as AES-GCM.");

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
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
    }

    private static void AnalyzeFieldReference(OperationAnalysisContext context)
    {
        var fieldReference = (IFieldReferenceOperation)context.Operation;
        var field = fieldReference.Field;

        if (ForbiddenModes.Contains(field.Name) &&
            FipsSymbolHelpers.IsCryptographyType(field.ContainingType, "CipherMode") &&
            !FipsSymbolHelpers.IsComparison(fieldReference))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Rule,
                fieldReference.Syntax.GetLocation(),
                field.Name));
        }
    }

    /// <summary>
    ///     Reports an explicit cast of a number to <c lang="csharp">CipherMode</c> whose constant is ECB (2), OFB (3)
    ///     or CFB (4), such as <c lang="csharp">(CipherMode)2</c>.
    /// </summary>
    private static void AnalyzeNumericCast(OperationAnalysisContext context)
    {
        var conversion = (IConversionOperation)context.Operation;
        if (conversion.IsImplicit ||
            !FipsSymbolHelpers.IsCryptographyType(conversion.Type, "CipherMode") ||
            conversion.Operand.Type?.TypeKind == TypeKind.Enum ||
            conversion.ConstantValue is not { HasValue: true, Value: { } value } ||
            ModeOfValue(Convert.ToInt64(value, CultureInfo.InvariantCulture)) is not { } mode ||
            FipsSymbolHelpers.IsComparison(conversion))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            conversion.Syntax.GetLocation(),
            mode));
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;

        var mode = ModeOfMethod(method.Name);
        if (mode is not null && FipsSymbolHelpers.IsCryptographyType(method.ContainingType, "SymmetricAlgorithm"))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Rule,
                invocation.Syntax.GetLocation(),
                mode));
        }
    }

    // CipherMode values: CBC is 1, ECB is 2, OFB is 3, CFB is 4 and CTS is 5.
    private static string? ModeOfValue(long value) => value switch
    {
        2 => "ECB",
        3 => "OFB",
        4 => "CFB",
        _ => null,
    };

    private static string? ModeOfMethod(string name)
    {
        switch (name)
        {
            case "EncryptEcb":
            case "DecryptEcb":
            case "TryEncryptEcb":
            case "TryDecryptEcb":
                return "ECB";
            case "EncryptCfb":
            case "DecryptCfb":
            case "TryEncryptCfb":
            case "TryDecryptCfb":
                return "CFB";
            default:
                return null;
        }
    }
}

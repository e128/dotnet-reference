using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace E128.Analyzers.Security;

/// <summary>
///     E128142: Do not instantiate a Managed or CNG cryptography type directly. Call the abstract factory so
///     the platform selects its implementation. Rijndael is reported because AES does not allow its block sizes.
///     A constructor that takes a key size or a curve reports the factory overload that keeps the argument.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FipsDirectInstantiationAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128142";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Do not instantiate a Managed or CNG cryptography type",
        "Do not instantiate '{0}' directly; call the factory '{1}' so the platform selects the implementation",
        "E128-Security",
        DiagnosticSeverity.Error,
        true,
        "A concrete Managed or Cng type fixes one implementation. The abstract factory lets the platform " +
        "choose its cryptographic module. A Cng constructor that takes a CngKey or a CngProvider " +
        "is not reported, because the abstract factory cannot open a persisted key.");

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

        context.RegisterOperationAction(AnalyzeObjectCreation, OperationKind.ObjectCreation);
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
    }

    /// <summary>
    ///     Reports the static <c lang="csharp">Rijndael.Create()</c> factory, which returns the same non-AES type.
    /// </summary>
    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;
        if (!method.IsStatic ||
            !string.Equals(method.Name, "Create", StringComparison.Ordinal) ||
            !FipsSymbolHelpers.IsCryptographyType(method.ContainingType, "Rijndael"))
        {
            return;
        }

        Report(context, invocation, "Rijndael", "Aes.Create()");
    }

    private static void AnalyzeObjectCreation(OperationAnalysisContext context)
    {
        var creation = (IObjectCreationOperation)context.Operation;
        var type = creation.Type;
        if (type is null)
        {
            return;
        }

        // E128071 already blocks these types, so factory advice would contradict it.
        if (FipsSymbolHelpers.IsCryptographyType(type, FipsForbiddenTypes.All) ||
            FipsTypeClassifier.GetVerdict(context.Compilation, type) is not TypeVerdict.Allowed)
        {
            return;
        }

        var name = type.Name;
        if (!FipsSymbolHelpers.IsInCryptographyNamespace(type))
        {
            ReportRijndaelOutsideCryptographyNamespace(context, creation, type);
            return;
        }

        if (IsRijndaelName(name))
        {
            Report(context, creation, name, "Aes.Create()");
            return;
        }

        if (!IsFactoryReplaceable(name, creation))
        {
            return;
        }

        Report(context, creation, name, SuggestFactory(name, creation) ?? "the abstract base factory's Create()");
    }

    // Rijndael can use a block size that AES does not allow. The name is ambiguous, so outside
    // System.Security.Cryptography only a symmetric algorithm counts.
    private static void ReportRijndaelOutsideCryptographyNamespace(
        OperationAnalysisContext context,
        IObjectCreationOperation creation,
        ITypeSymbol type)
    {
        if (!IsRijndaelName(type.Name) ||
            !FipsSymbolHelpers.DerivesFromCryptographyType(type, "SymmetricAlgorithm"))
        {
            return;
        }

        Report(context, creation, type.Name, "Aes.Create()");
    }

    private static bool IsRijndaelName(string name)
    {
        return string.Equals(name, "Rijndael", StringComparison.Ordinal) ||
               string.Equals(name, "RijndaelManaged", StringComparison.Ordinal);
    }

    private static bool IsFactoryReplaceable(string name, IObjectCreationOperation creation)
    {
        return name.EndsWith("Managed", StringComparison.Ordinal) ||
               (name.EndsWith("Cng", StringComparison.Ordinal) && !UsesCngKeyStorage(creation.Constructor));
    }

    /// <summary>
    ///     Returns true for a Cng constructor that takes a <c lang="csharp">CngKey</c> or a <c lang="csharp">CngProvider</c>.
    ///     Those constructors open a persisted or hardware-backed key, which the abstract factory cannot do.
    /// </summary>
    private static bool UsesCngKeyStorage(IMethodSymbol? constructor)
    {
        if (constructor is null)
        {
            return false;
        }

        foreach (var parameter in constructor.Parameters)
        {
            if (FipsSymbolHelpers.IsCryptographyType(parameter.Type, "CngKey") ||
                FipsSymbolHelpers.IsCryptographyType(parameter.Type, "CngProvider"))
            {
                return true;
            }
        }

        return false;
    }

    private static void Report(OperationAnalysisContext context, IOperation operation, string typeName, string suggestion)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            operation.Syntax.GetLocation(),
            typeName,
            suggestion));
    }

    /// <summary>
    ///     Best-effort factory suggestion: AesManaged becomes Aes.Create(), SHA256Cng becomes SHA256.Create(),
    ///     <c lang="csharp">new RSACng(3072)</c> becomes <c lang="csharp">RSA.Create(3072)</c> and
    ///     <c lang="csharp">new ECDsaCng(curve)</c> becomes <c lang="csharp">ECDsa.Create(curve)</c>.
    ///     Returns null when the constructor takes arguments that no factory overload accepts.
    /// </summary>
    private static string? SuggestFactory(string typeName, IObjectCreationOperation creation)
    {
        var baseName = StripFactorySuffix(typeName);
        if (creation.Arguments.Length is 0)
        {
            return baseName.Length is 0 ? "the abstract base factory's Create()" : baseName + ".Create()";
        }

        if (creation.Arguments.Length is not 1 || creation.Arguments[0].Parameter?.Type is not { } parameterType)
        {
            return null;
        }

        return KeepsFactoryArgument(baseName, parameterType)
            ? baseName + ".Create(" + creation.Arguments[0].Value.Syntax + ")"
            : null;
    }

    private static string StripFactorySuffix(string typeName)
    {
        if (typeName.EndsWith("Managed", StringComparison.Ordinal))
        {
            return typeName.Substring(0, typeName.Length - "Managed".Length);
        }

        if (typeName.EndsWith("Cng", StringComparison.Ordinal))
        {
            return typeName.Substring(0, typeName.Length - "Cng".Length);
        }

        return typeName;
    }

    private static bool KeepsFactoryArgument(string baseName, ITypeSymbol parameterType)
    {
        var keepsKeySize =
            (string.Equals(baseName, "RSA", StringComparison.Ordinal) ||
             string.Equals(baseName, "DSA", StringComparison.Ordinal)) &&
            parameterType.SpecialType is SpecialType.System_Int32;
        var keepsCurve =
            (string.Equals(baseName, "ECDsa", StringComparison.Ordinal) ||
             string.Equals(baseName, "ECDiffieHellman", StringComparison.Ordinal)) &&
            FipsSymbolHelpers.IsCryptographyType(parameterType, "ECCurve");

        return keepsKeySize || keepsCurve;
    }
}

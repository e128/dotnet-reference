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
        "Security",
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

    /// <summary>Reports the static <c lang="csharp">Rijndael.Create()</c> factory, which returns the same non-AES type.</summary>
    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;
        if (method.IsStatic &&
            string.Equals(method.Name, "Create", StringComparison.Ordinal) &&
            FipsSymbolHelpers.IsCryptographyType(method.ContainingType, "Rijndael"))
        {
            Report(context, invocation, "Rijndael", "Aes.Create()");
        }
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
        if (FipsSymbolHelpers.IsCryptographyType(type, FipsForbiddenTypes.All))
        {
            return;
        }

        var name = type.Name;
        if (!FipsSymbolHelpers.IsInCryptographyNamespace(type))
        {
            // Rijndael can use a block size that AES does not allow. The name is ambiguous, so outside
            // System.Security.Cryptography only a symmetric algorithm counts.
            if ((string.Equals(name, "Rijndael", StringComparison.Ordinal) ||
                 string.Equals(name, "RijndaelManaged", StringComparison.Ordinal)) &&
                FipsSymbolHelpers.DerivesFromCryptographyType(type, "SymmetricAlgorithm"))
            {
                Report(context, creation, name, "Aes.Create()");
            }

            return;
        }

        if (string.Equals(name, "Rijndael", StringComparison.Ordinal) ||
            string.Equals(name, "RijndaelManaged", StringComparison.Ordinal))
        {
            Report(context, creation, name, "Aes.Create()");
            return;
        }

        if (name.EndsWith("Managed", StringComparison.Ordinal) ||
            (name.EndsWith("Cng", StringComparison.Ordinal) && !UsesCngKeyStorage(creation.Constructor)))
        {
            Report(context, creation, name, SuggestFactory(name, creation) ?? "the abstract base factory's Create()");
        }
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

    private static void Report(OperationAnalysisContext context, IOperation operation, string typeName, string suggestion) =>
        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            operation.Syntax.GetLocation(),
            typeName,
            suggestion));

    /// <summary>
    ///     Best-effort factory suggestion: AesManaged becomes Aes.Create(), SHA256Cng becomes SHA256.Create(),
    ///     <c lang="csharp">new RSACng(3072)</c> becomes <c lang="csharp">RSA.Create(3072)</c> and
    ///     <c lang="csharp">new ECDsaCng(curve)</c> becomes <c lang="csharp">ECDsa.Create(curve)</c>.
    ///     Returns null when the constructor takes arguments that no factory overload accepts.
    /// </summary>
    private static string? SuggestFactory(string typeName, IObjectCreationOperation creation)
    {
        var baseName = typeName;
        if (baseName.EndsWith("Managed", StringComparison.Ordinal))
        {
            baseName = baseName.Substring(0, baseName.Length - "Managed".Length);
        }
        else if (baseName.EndsWith("Cng", StringComparison.Ordinal))
        {
            baseName = baseName.Substring(0, baseName.Length - "Cng".Length);
        }

        if (creation.Arguments.Length == 0)
        {
            return baseName.Length == 0 ? "the abstract base factory's Create()" : baseName + ".Create()";
        }

        if (creation.Arguments.Length != 1 || creation.Arguments[0].Parameter?.Type is not { } parameterType)
        {
            return null;
        }

        var keepsKeySize = baseName is "RSA" or "DSA" && parameterType.SpecialType == SpecialType.System_Int32;
        var keepsCurve = baseName is "ECDsa" or "ECDiffieHellman" &&
            FipsSymbolHelpers.IsCryptographyType(parameterType, "ECCurve");

        return keepsKeySize || keepsCurve
            ? baseName + ".Create(" + creation.Arguments[0].Value.Syntax + ")"
            : null;
    }
}

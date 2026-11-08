using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace E128.Analyzers.Security;

/// <summary>
///     E128071: Reports algorithms and members that organizational cryptography policy blocks: MD5, SHA-1,
///     RIPEMD-160, DES, RC2, Triple DES, the MAC types built on them, ChaCha20Poly1305, PasswordDeriveBytes and
///     DSA signature generation. The rule covers the types, their static Create and HashData members, legacy
///     factories that select a weak algorithm by default, factories that take a constant algorithm name, and
///     any cryptography API that takes a forbidden <c lang="csharp">HashAlgorithmName</c>. Verification methods are skipped,
///     so code can still check an existing signature.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FipsUnapprovedHashAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128071";

    private static readonly ImmutableHashSet<string> ForbiddenAlgorithmTypes = FipsForbiddenTypes.Algorithms;

    /// <summary>
    ///     Algorithm names that a factory, a hash-name argument or an XML-DSig and XML-Enc property resolves to a
    ///     forbidden algorithm. The comparison ignores case, and a leading <c lang="csharp">System.Security.Cryptography.</c>
    ///     is removed first.
    /// </summary>
    private static readonly ImmutableHashSet<string> LegacyDefaultFactoryTypes = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "HashAlgorithm",
        "HMAC",
        "KeyedHashAlgorithm");

    private static readonly ImmutableHashSet<string> ForbiddenAlgorithmNames =
        ForbiddenAlgorithmTypes.Union(LegacyDefaultFactoryTypes).Union(new[]
        {
            "SHA",
            "SHA-1",
            "RIPEMD",
            "RIPEMD-160",
            "3DES",
            "Triple DES",
            "http://www.w3.org/2000/09/xmldsig#sha1",
            "http://www.w3.org/2001/04/xmldsig-more#md5",
            "http://www.w3.org/2001/04/xmldsig-more#ripemd160",
            "http://www.w3.org/2000/09/xmldsig#hmac-sha1",
            "http://www.w3.org/2001/04/xmldsig-more#hmac-md5",
            "http://www.w3.org/2001/04/xmldsig-more#hmac-ripemd160",
            "http://www.w3.org/2000/09/xmldsig#rsa-sha1",
            "http://www.w3.org/2000/09/xmldsig#dsa-sha1",
            "http://www.w3.org/2001/04/xmlenc#des-cbc",
            "http://www.w3.org/2001/04/xmlenc#tripledes-cbc",
            "http://www.w3.org/2001/04/xmlenc#kw-tripledes",
            "http://www.w3.org/2001/04/xmlenc#rsa-oaep-mgf1p",
            "http://www.w3.org/2001/04/xmlenc#rsa-1_5",
        }).WithComparer(StringComparer.OrdinalIgnoreCase);

    private const string XmlDsigNamespace = "System.Security.Cryptography.Xml";

    private const string PkcsNamespace = "System.Security.Cryptography.Pkcs";

    private const string Pkcs1Encryption = "RSA PKCS#1 v1.5 encryption (use RSAEncryptionPadding.OaepSHA256)";

    private static readonly ImmutableHashSet<string> Pbkdf2Types = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "Rfc2898DeriveBytes",
        "PbeParameters");

    private static readonly ImmutableHashSet<string> OaepKeyExchangeTypes = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "RSAOAEPKeyExchangeFormatter",
        "RSAOAEPKeyExchangeDeformatter");

    private static readonly ImmutableHashSet<string> Pkcs1KeyExchangeTypes = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "RSAPKCS1KeyExchangeFormatter",
        "RSAPKCS1KeyExchangeDeformatter");

    private static readonly ImmutableHashSet<string> RsaEncryptionMembers = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "Encrypt",
        "Decrypt",
        "TryEncrypt",
        "TryDecrypt");

    private static readonly ImmutableHashSet<string> NamedFactoryMethods = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "Create",
        "CreateFromName");

    private static readonly ImmutableHashSet<string> DsaSigningMembers = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "CreateSignature",
        "TryCreateSignature",
        "SignData",
        "TrySignData",
        "SignHash");

    // Legacy signing members that take the hash as a string, an object or a Type.
    private static readonly ImmutableHashSet<string> LegacyHashNameMembers = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "SignData",
        "SignHash",
        "SetHashAlgorithm");

    // Object identifiers for SHA-1, MD5 and RIPEMD-160.
    private static readonly ImmutableHashSet<string> ForbiddenHashOids = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "1.3.14.3.2.26",
        "1.2.840.113549.2.5",
        "1.3.36.3.2.1");

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Use an approved cryptographic algorithm",
        "'{0}' is blocked by the cryptography policy; use SHA-256, SHA-384 or SHA-512 for hashing and AES for encryption",
        "E128-Security",
        DiagnosticSeverity.Error,
        true,
        "The policy blocks MD5, SHA-1, RIPEMD-160, DES, RC2, Triple DES, the MAC types built on them, " +
        "ChaCha20Poly1305 and PasswordDeriveBytes. DSA signature generation is reported and DSA verification " +
        "is allowed, because NIST FIPS 186-5 permits DSA only to verify existing signatures.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();

        context.RegisterOperationAction(AnalyzeObjectCreation, OperationKind.ObjectCreation);
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
        context.RegisterOperationAction(AnalyzePropertyReference, OperationKind.PropertyReference);
        context.RegisterOperationAction(AnalyzeMethodReference, OperationKind.MethodReference);
        context.RegisterOperationAction(AnalyzeFieldReference, OperationKind.FieldReference);
        context.RegisterOperationAction(
            AnalyzeHashNameStore,
            OperationKind.SimpleAssignment,
            OperationKind.CoalesceAssignment,
            OperationKind.DeconstructionAssignment,
            OperationKind.FieldInitializer,
            OperationKind.PropertyInitializer);
        context.RegisterOperationAction(
            AnalyzeAssignment,
            OperationKind.SimpleAssignment,
            OperationKind.CoalesceAssignment,
            OperationKind.DeconstructionAssignment);
    }

    /// <summary>
    ///     Reports a constant forbidden XML-DSig or XML-Enc URI assigned to <c lang="csharp">SignedInfo.SignatureMethod</c>,
    ///     <c lang="csharp">Reference.DigestMethod</c> or <c lang="csharp">EncryptionMethod.KeyAlgorithm</c>,
    ///     and a forbidden hash object identifier assigned to <c lang="csharp">CmsSigner.DigestAlgorithm</c>.
    /// </summary>
    /// <remarks>
    ///     The target member name decides whether the assignment can report. A deconstruction
    ///     assignment always enters the walk, because its targets appear only after the split.
    /// </remarks>
    private static void AnalyzeAssignment(OperationAnalysisContext context)
    {
        var mayTargetAlgorithm = context.Operation switch
        {
            ISimpleAssignmentOperation assignment => IsAlgorithmPropertyTarget(assignment.Target),
            ICoalesceAssignmentOperation assignment => IsAlgorithmPropertyTarget(assignment.Target),
            IDeconstructionAssignmentOperation => true,
            _ => false,
        };

        if (mayTargetAlgorithm)
        {
            FipsSymbolHelpers.ForEachAssignment(
                context.Operation,
                context,
                static (state, target, value, syntax) => CheckXmlUriAssignment(state, target, value, syntax));
        }
    }

    private static bool IsAlgorithmPropertyTarget(IOperation target) =>
        FipsSymbolHelpers.IsCandidateAssignmentTarget(target, "SignatureMethod") ||
        FipsSymbolHelpers.IsCandidateAssignmentTarget(target, "DigestMethod") ||
        FipsSymbolHelpers.IsCandidateAssignmentTarget(target, "KeyAlgorithm") ||
        FipsSymbolHelpers.IsCandidateAssignmentTarget(target, "DigestAlgorithm");

    private static void CheckXmlUriAssignment(
        OperationAnalysisContext context,
        IOperation target,
        IOperation value,
        SyntaxNode syntax)
    {
        if (target is not IPropertyReferenceOperation { Property: { } property })
        {
            return;
        }

        if (string.Equals(property.Name, "DigestAlgorithm", StringComparison.Ordinal) &&
            FipsSymbolHelpers.IsType(property.ContainingType, "CmsSigner", PkcsNamespace))
        {
            // CmsSigner.DigestAlgorithm = new Oid("1.3.14.3.2.26")
            if (FipsSymbolHelpers.Unwrap(value) is IObjectCreationOperation oid &&
                FipsSymbolHelpers.IsCryptographyType(oid.Type, "Oid") &&
                FipsSymbolHelpers.TryGetConstant<string>(FipsSymbolHelpers.ArgumentAt(oid.Arguments, 0)?.Value, out var oidValue) &&
                ForbiddenHashOids.Contains(oidValue.Trim()))
            {
                ReportForbidden(context, syntax, oidValue);
            }

            return;
        }

        if (!((string.Equals(property.Name, "SignatureMethod", StringComparison.Ordinal) &&
               FipsSymbolHelpers.IsType(property.ContainingType, "SignedInfo", XmlDsigNamespace)) ||
              (string.Equals(property.Name, "DigestMethod", StringComparison.Ordinal) &&
               FipsSymbolHelpers.IsType(property.ContainingType, "Reference", XmlDsigNamespace)) ||
              (string.Equals(property.Name, "KeyAlgorithm", StringComparison.Ordinal) &&
               FipsSymbolHelpers.IsType(property.ContainingType, "EncryptionMethod", XmlDsigNamespace))))
        {
            return;
        }

        if (FipsSymbolHelpers.TryGetConstant<string>(value, out var uri) && IsForbiddenAlgorithmName(uri))
        {
            ReportForbidden(context, syntax, uri);
        }
    }

    private static void AnalyzeObjectCreation(OperationAnalysisContext context)
    {
        var creation = (IObjectCreationOperation)context.Operation;
        var syntax = creation.Syntax;

        if (FipsSymbolHelpers.IsCryptographyType(creation.Type, "DSASignatureFormatter"))
        {
            ReportForbidden(context, syntax, "DSA signature generation (DSASignatureFormatter)");
            return;
        }

        // RSAOAEPKeyExchangeFormatter and its deformatter always use OAEP with SHA-1.
        if (FipsSymbolHelpers.IsCryptographyType(creation.Type, OaepKeyExchangeTypes))
        {
            ReportForbidden(context, syntax, "SHA1 (RSA OAEP padding)");
            return;
        }

        // RSAPKCS1KeyExchangeFormatter and its deformatter always use PKCS#1 v1.5 padding.
        if (FipsSymbolHelpers.IsCryptographyType(creation.Type, Pkcs1KeyExchangeTypes))
        {
            ReportForbidden(context, syntax, Pkcs1Encryption);
            return;
        }

        // new EncryptionMethod("http://www.w3.org/2001/04/xmlenc#tripledes-cbc")
        if (FipsSymbolHelpers.IsType(creation.Type, "EncryptionMethod", XmlDsigNamespace) &&
            FipsSymbolHelpers.TryGetConstant<string>(FipsSymbolHelpers.ArgumentAt(creation.Arguments, 0)?.Value, out var uri) &&
            IsForbiddenAlgorithmName(uri))
        {
            ReportForbidden(context, syntax, uri);
            return;
        }

        // PBKDF2 and PBE hash arguments belong to E128145.
        if (!FipsSymbolHelpers.IsCryptographyType(creation.Type, Pbkdf2Types))
        {
            ReportHashNameArguments(context, creation.Constructor, creation.Arguments, syntax);
        }

        InspectType(context, creation.Type, syntax);
    }

    /// <summary>
    ///     Reports <c lang="csharp">PbeEncryptionAlgorithm.TripleDes3KeyPkcs12</c>, the Triple DES password-based encryption of
    ///     a PKCS#8 or PKCS#12 export. A comparison is not a selection.
    /// </summary>
    private static void AnalyzeFieldReference(OperationAnalysisContext context)
    {
        var reference = (IFieldReferenceOperation)context.Operation;
        if (string.Equals(reference.Field.Name, "TripleDes3KeyPkcs12", StringComparison.Ordinal) &&
            FipsSymbolHelpers.IsCryptographyType(reference.Field.ContainingType, "PbeEncryptionAlgorithm") &&
            !FipsSymbolHelpers.IsComparison(reference))
        {
            ReportForbidden(context, reference.Syntax, "TripleDES (PbeEncryptionAlgorithm.TripleDes3KeyPkcs12)");
        }
    }

    /// <summary>
    ///     Reports <c lang="csharp">RSAEncryptionPadding.OaepSHA1</c> everywhere, and
    ///     <c lang="csharp">RSAEncryptionPadding.Pkcs1</c> when an RSA <c lang="csharp">Encrypt</c>, <c lang="csharp">Decrypt</c>,
    ///     <c lang="csharp">TryEncrypt</c> or <c lang="csharp">TryDecrypt</c> call receives it as an argument.
    ///     <c lang="csharp">RSASignaturePadding.Pkcs1</c> is a different type and stays allowed.
    /// </summary>
    private static void AnalyzePropertyReference(OperationAnalysisContext context)
    {
        var reference = (IPropertyReferenceOperation)context.Operation;
        var property = reference.Property;

        if (!property.IsStatic || !FipsSymbolHelpers.IsCryptographyType(property.ContainingType, "RSAEncryptionPadding"))
        {
            return;
        }

        if (string.Equals(property.Name, "OaepSHA1", StringComparison.Ordinal))
        {
            ReportForbidden(context, reference.Syntax, "SHA1 (RSA OAEP padding)");
        }
        else if (string.Equals(property.Name, "Pkcs1", StringComparison.Ordinal) &&
                 reference.Parent is IArgumentOperation { Parent: IInvocationOperation invocation } &&
                 RsaEncryptionMembers.Contains(invocation.TargetMethod.Name) &&
                 FipsSymbolHelpers.DerivesFromCryptographyType(invocation.TargetMethod.ContainingType, "RSA"))
        {
            ReportForbidden(context, reference.Syntax, Pkcs1Encryption);
        }
    }

    /// <summary>
    ///     Reports a method group that names a static factory or one-shot helper of a forbidden type, such as
    ///     <c lang="csharp">Func&lt;MD5&gt; create = MD5.Create</c>.
    /// </summary>
    private static void AnalyzeMethodReference(OperationAnalysisContext context)
    {
        var reference = (IMethodReferenceOperation)context.Operation;
        if (reference.Method.IsStatic && FipsTypeClassifier.OneShotMembers.Contains(reference.Method.Name))
        {
            InspectType(context, reference.Method.ContainingType, reference.Syntax);
        }
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;

        // One diagnostic per call: a SHA-1 hash name on the same call would repeat it.
        if (ReportDsaOrOaep(context, invocation) ||
            ReportLegacyHashName(context, invocation))
        {
            return;
        }

        // Verification of an existing signature stays allowed, and PBKDF2 belongs to E128145.
        if (!IsExemptFromHashNameCheck(method))
        {
            ReportHashNameArguments(context, method, invocation.Arguments, invocation.Syntax);
        }

        if (method.IsStatic)
        {
            AnalyzeStaticInvocation(context, invocation);
        }
    }

    private static bool ReportDsaOrOaep(OperationAnalysisContext context, IInvocationOperation invocation)
    {
        var method = invocation.TargetMethod;
        var syntax = invocation.Syntax;

        if (DsaSigningMembers.Contains(method.Name) &&
            FipsSymbolHelpers.DerivesFromCryptographyType(method.ContainingType, "DSA"))
        {
            ReportForbidden(context, syntax, $"DSA signature generation ({method.Name})");

            return true;
        }

        // RSACryptoServiceProvider.Encrypt(data, fOAEP) and Decrypt: true selects OAEP with SHA-1, false selects
        // PKCS#1 v1.5, and a flag that is not constant can select either.
        if ((string.Equals(method.Name, "Encrypt", StringComparison.Ordinal) ||
             string.Equals(method.Name, "Decrypt", StringComparison.Ordinal)) &&
            FipsSymbolHelpers.IsCryptographyType(method.ContainingType, "RSACryptoServiceProvider") &&
            FipsSymbolHelpers.ArgumentNamed(invocation.Arguments, "fOAEP") is { } oaepFlag)
        {
            ReportForbidden(
                context,
                syntax,
                oaepFlag.Value.ConstantValue switch
                {
                    { HasValue: true, Value: true } => "SHA1 (RSA OAEP padding)",
                    { HasValue: true } => Pkcs1Encryption,
                    _ => "SHA1 (RSA OAEP padding) or RSA PKCS#1 v1.5 encryption, when fOAEP is not constant (use RSAEncryptionPadding.OaepSHA256)",
                });

            return true;
        }

        return false;
    }

    private static bool ReportLegacyHashName(OperationAnalysisContext context, IInvocationOperation invocation)
    {
        var method = invocation.TargetMethod;
        if (method.IsStatic ||
            !LegacyHashNameMembers.Contains(method.Name) ||
            !FipsSymbolHelpers.IsInCryptographyNamespace(method.ContainingType) ||
            method.ContainingType.Name.IndexOf("Deformatter", StringComparison.Ordinal) >= 0 ||
            FipsSymbolHelpers.IsCryptographyType(method.ContainingType, "DSASignatureFormatter") ||
            !TryGetLegacyHashName(invocation.Arguments, out var legacyHash))
        {
            return false;
        }

        ReportForbidden(context, invocation.Syntax, legacyHash);

        return true;
    }

    /// <summary>
    ///     Returns true for the methods whose hash-name arguments the rule does not check. Verification of an
    ///     existing signature stays allowed, but only for a framework method: <c lang="csharp">RSA.VerifyData</c>
    ///     qualifies and an application's <c lang="csharp">VerifyAndSign</c> does not. The PBKDF2 hash arguments of
    ///     <c lang="csharp">Rfc2898DeriveBytes.Pbkdf2</c> and <c lang="csharp">KeyDerivation.Pbkdf2</c> belong to
    ///     E128145, so a method that merely shares the name <c lang="csharp">Pbkdf2</c> is checked.
    /// </summary>
    private static bool IsExemptFromHashNameCheck(IMethodSymbol method)
    {
        var owner = method.ContainingType;

        return string.Equals(method.Name, "Pbkdf2", StringComparison.Ordinal)
            ? FipsSymbolHelpers.IsCryptographyType(owner, "Rfc2898DeriveBytes") ||
                FipsSymbolHelpers.IsType(owner, "KeyDerivation", FipsSymbolHelpers.KeyDerivation)
            : FipsSymbolHelpers.DerivesFromCryptographyType(owner, "AsymmetricAlgorithm") &&
                (method.Name.StartsWith("Verify", StringComparison.Ordinal) ||
                 method.Name.StartsWith("TryVerify", StringComparison.Ordinal));
    }

    private static void AnalyzeStaticInvocation(OperationAnalysisContext context, IInvocationOperation invocation)
    {
        var method = invocation.TargetMethod;
        var syntax = invocation.Syntax;

        // Static members called on a forbidden type without 'new', such as MD5.Create() and MD5.HashData().
        if (FipsTypeClassifier.OneShotMembers.Contains(method.Name) && InspectType(context, method.ContainingType, syntax))
        {
            return;
        }

        if (!FipsSymbolHelpers.IsInCryptographyNamespace(method.ContainingType))
        {
            return;
        }

        // HashAlgorithm.Create() and friends select SHA-1 or HMAC-SHA1 by default.
        if (string.Equals(method.Name, "Create", StringComparison.Ordinal) &&
            method.Parameters.Length == 0 &&
            LegacyDefaultFactoryTypes.Contains(method.ContainingType.Name))
        {
            var text = string.Equals(method.ContainingType.Name, "HashAlgorithm", StringComparison.Ordinal)
                ? "HashAlgorithm.Create() (legacy factory that selects SHA-1 by default)"
                : method.ContainingType.Name + ".Create() (legacy factory that selects HMAC-SHA1 by default)";
            ReportForbidden(context, syntax, text);

            return;
        }

        // Factories that resolve an algorithm from a name, such as HashAlgorithm.Create("MD5").
        if (NamedFactoryMethods.Contains(method.Name) &&
            method.Parameters.Length > 0 &&
            method.Parameters[0].Type.SpecialType == SpecialType.System_String &&
            FipsSymbolHelpers.TryGetConstant<string>(FipsSymbolHelpers.ArgumentAt(invocation.Arguments, 0)?.Value, out var name) &&
            IsForbiddenAlgorithmName(name))
        {
            ReportForbidden(context, syntax, name);
        }
    }

    /// <summary>
    ///     Reads a forbidden hash from a string, object or Type argument of a legacy signing member: a constant
    ///     name (<c lang="csharp">"SHA1"</c>), an object identifier (<c lang="csharp">"1.3.14.3.2.26"</c>) or <c lang="csharp">typeof(SHA1)</c>.
    /// </summary>
    private static bool TryGetLegacyHashName(ImmutableArray<IArgumentOperation> arguments, out string name)
    {
        foreach (var argument in arguments)
        {
            var parameterType = argument.Parameter?.Type.SpecialType;
            if (parameterType is not (SpecialType.System_String or SpecialType.System_Object))
            {
                continue;
            }

            var value = FipsSymbolHelpers.Unwrap(argument.Value);

            // CryptoConfig.MapNameToOID("SHA1") is the object identifier of the named algorithm.
            if (value is IInvocationOperation { TargetMethod: { IsStatic: true, Name: "MapNameToOID" } mapper } map &&
                FipsSymbolHelpers.IsCryptographyType(mapper.ContainingType, "CryptoConfig"))
            {
                value = FipsSymbolHelpers.Unwrap(FipsSymbolHelpers.ArgumentAt(map.Arguments, 0)?.Value);
            }

            if (FipsSymbolHelpers.TryGetConstant<string>(value, out var constantName) &&
                (IsForbiddenAlgorithmName(constantName) || ForbiddenHashOids.Contains(constantName.Trim())))
            {
                name = constantName;

                return true;
            }

            if (value is ITypeOfOperation typeOf &&
                FipsSymbolHelpers.IsCryptographyType(typeOf.TypeOperand, FipsForbiddenTypes.All))
            {
                name = typeOf.TypeOperand.Name;
                return true;
            }
        }

        name = string.Empty;
        return false;
    }

    private static void ReportHashNameArguments(
        OperationAnalysisContext context,
        IMethodSymbol? method,
        ImmutableArray<IArgumentOperation> arguments,
        SyntaxNode syntax) =>
        FipsSymbolHelpers.ReportHashNameArguments(
            method,
            arguments,
            syntax,
            context,
            static (_, name) => IsForbiddenAlgorithmName(name),
            static (state, node, name) => ReportForbidden(state, node, name));

    /// <summary>
    ///     Reports a forbidden hash name stored in a mutable field, a property or a reassigned target, because a
    ///     later read of that target cannot be followed.
    /// </summary>
    private static void AnalyzeHashNameStore(OperationAnalysisContext context) =>
        FipsSymbolHelpers.ReportHashNameStores(
            context.Operation,
            context,
            static (_, name) => IsForbiddenAlgorithmName(name),
            static (state, node, name) => ReportForbidden(state, node, name));

    private static void ReportForbidden(OperationAnalysisContext context, SyntaxNode syntax, string algorithm) =>
        context.ReportDiagnostic(Diagnostic.Create(Rule, syntax.GetLocation(), algorithm));

    /// <summary>
    ///     Reports a forbidden type or a type that derives from one. Returns true when either type rule owns the
    ///     type, so E128148 alone reports a cipher that matches by name.
    /// </summary>
    private static bool InspectType(OperationAnalysisContext context, ITypeSymbol? type, SyntaxNode syntax)
    {
        if (type is null)
        {
            return false;
        }

        var verdict = FipsTypeClassifier.GetVerdict(context.Compilation, type);
        if (verdict == TypeVerdict.ForbiddenType)
        {
            ReportForbidden(context, syntax, FipsSymbolHelpers.FullName(type) ?? type.Name);
        }

        return verdict != TypeVerdict.Allowed;
    }

    private static bool IsForbiddenAlgorithmName(string name) =>
        ForbiddenAlgorithmNames.Contains(NormalizeName(name));

    private static string NormalizeName(string name)
    {
        name = name.Trim();

        const string prefix = FipsSymbolHelpers.Cryptography + ".";
        if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            name = name.Substring(prefix.Length);
        }

        return name;
    }
}

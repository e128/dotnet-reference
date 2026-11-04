using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace E128.Analyzers.Security;

/// <summary>
///     E128143: Reports RSA keys under 3072 bits and elliptic curve keys under 384 bits. These limits are
///     stricter organizational policy than NIST guidance. An RSA key size that cannot be verified at compile
///     time is also reported.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FipsRsaKeySizeAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128143";

    private const int MinRsaBits = 3072;
    private const int MinEllipticCurveBits = 384;

    private static readonly string MinRsaBitsText = MinRsaBits.ToString(CultureInfo.InvariantCulture);

    private static readonly string NamedDefaultKeySizeMessage =
        $"RSA created by name uses the platform default key size, which cannot be verified; create the key with an explicit key size of at least {MinRsaBitsText} bits";

    /// <summary>Curve names and object identifiers for P-384 and P-521. The comparison ignores case.</summary>
    private static readonly ImmutableHashSet<string> AllowedCurves = ImmutableHashSet.Create(
        StringComparer.OrdinalIgnoreCase,
        "nistP384",
        "nistP521",
        "secp384r1",
        "secp521r1",
        "P-384",
        "P-521",
        "ECDSA_P384",
        "ECDSA_P521",
        "ECDH_P384",
        "ECDH_P521",
        "1.3.132.0.34",
        "1.3.132.0.35");

    private static readonly ImmutableHashSet<string> ImportMethods = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "ImportParameters",
        "ImportSubjectPublicKeyInfo",
        "ImportRSAPublicKey",
        "ImportRSAPrivateKey",
        "ImportPkcs8PrivateKey",
        "ImportEncryptedPkcs8PrivateKey",
        "ImportFromPem",
        "ImportFromEncryptedPem",
        "ImportCspBlob",
        "FromXmlString");

    private static readonly ImmutableHashSet<string> ImportedKeyTypes = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "RSAParameters",
        "CngKey",
        "SafeEvpPKeyHandle");

    private static readonly ImmutableHashSet<string> EcImportMethods = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "ImportSubjectPublicKeyInfo",
        "ImportECPrivateKey",
        "ImportPkcs8PrivateKey",
        "ImportEncryptedPkcs8PrivateKey",
        "ImportFromPem",
        "ImportFromEncryptedPem");

    private static readonly ImmutableHashSet<string> CertificateKeyMethods = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "GetRSAPublicKey",
        "GetRSAPrivateKey",
        "GetECDsaPublicKey",
        "GetECDsaPrivateKey",
        "GetECDiffieHellmanPublicKey",
        "GetECDiffieHellmanPrivateKey");

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Use an RSA key of at least 3072 bits or an approved curve",
        "{0}",
        "E128-Security",
        DiagnosticSeverity.Error,
        true,
        "Policy requires RSA keys of at least 3072 bits and elliptic curves of at least 384 bits (P-384 or " +
        "P-521). An RSA key size that cannot be determined at compile time is reported, including a key " +
        "imported from parameters, a handle, a blob, PEM text or a certificate.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        // Cryptographic key creation is reported in generated code too, because a generated key setup is a real
        // policy defect. FIPS0003 parity requires this opt-in.
        context.ConfigureGeneratedCodeAnalysis(
            GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(start =>
        {
            // .NET Framework's mscorlib defines object. There RSA.Create() returns RSACryptoServiceProvider.
            var isFramework =
                string.Equals(
                    start.Compilation.GetSpecialType(SpecialType.System_Object).ContainingAssembly?.Name,
                    "mscorlib",
                    StringComparison.Ordinal);

            start.RegisterOperationAction(operation => AnalyzeInvocation(operation, isFramework), OperationKind.Invocation);
            start.RegisterOperationAction(operation => AnalyzeObjectCreation(operation, isFramework), OperationKind.ObjectCreation);
            start.RegisterOperationAction(AnalyzeMethodReference, OperationKind.MethodReference);
            start.RegisterOperationAction(
                AnalyzeAssignment,
                OperationKind.SimpleAssignment,
                OperationKind.CoalesceAssignment,
                OperationKind.CompoundAssignment,
                OperationKind.DeconstructionAssignment);
        });
    }

    private static Family GetFamily(ITypeSymbol? type)
    {
        var isRsa = FipsSymbolHelpers.DerivesFromCryptographyType(type, "RSA");
        var isEllipticCurve = FipsSymbolHelpers.DerivesFromCryptographyType(type, "ECDsa") ||
            FipsSymbolHelpers.DerivesFromCryptographyType(type, "ECDiffieHellman");

        return isRsa ? Family.Rsa : isEllipticCurve ? Family.EllipticCurve : Family.None;
    }

    /// <summary>
    ///     Reports a certificate key, whose size or curve comes from the certificate and cannot be verified at
    ///     compile time. Returns true when the call is one.
    /// </summary>
    private static bool ReportCertificateKey(OperationAnalysisContext context, IInvocationOperation invocation)
    {
        var method = invocation.TargetMethod;
        if (!CertificateKeyMethods.Contains(method.Name) ||
            !FipsSymbolHelpers.IsInCryptographyNamespaceTree(method.ContainingType))
        {
            return false;
        }

        var message = GetFamily(method.ReturnType) switch
        {
            Family.Rsa => ImportedKeySizeMessage(method.ReturnType.Name),
            Family.EllipticCurve => ImportedCurveMessage(method.ReturnType.Name),
            Family.None => null,
            _ => null,
        };

        if (message is null)
        {
            return false;
        }

        Report(context, invocation.Syntax, message);

        return true;
    }

    /// <summary>
    ///     Checks the static factory calls <c lang="csharp">RSA.Create</c>, <c lang="csharp">ECDsa.Create</c> and
    ///     <c lang="csharp">ECDiffieHellman.Create</c>. Returns true when the call is one of them.
    /// </summary>
    private static bool AnalyzeStaticCreate(OperationAnalysisContext context, IInvocationOperation invocation, bool isFramework)
    {
        var method = invocation.TargetMethod;
        if (!method.IsStatic ||
            !string.Equals(method.Name, "Create", StringComparison.Ordinal) ||
            !FipsSymbolHelpers.IsInCryptographyNamespace(method.ContainingType))
        {
            return false;
        }

        var type = method.ContainingType;
        if (FipsSymbolHelpers.IsCryptographyType(type, "RSA"))
        {
            CheckRsaCreation(context, invocation, invocation.Arguments, type.Name, invocation.Syntax, isFramework);
        }
        else if ((FipsSymbolHelpers.IsCryptographyType(type, "ECDsa") ||
                  FipsSymbolHelpers.IsCryptographyType(type, "ECDiffieHellman")) &&
                 // A factory without a curve argument creates the default P-256 key, which is below the policy minimum.
                 !CheckCurveArgument(context, invocation.Arguments, invocation.Syntax))
        {
            Report(context, invocation.Syntax, DefaultCurveMessage(type.Name));
        }

        return true;
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context, bool isFramework)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;
        var syntax = invocation.Syntax;

        if (ReportCertificateKey(context, invocation))
        {
            return;
        }

        if (IsRsaCreatedByName(invocation))
        {
            // The returned object is not an RSA type here, so a later KeySize assignment is not tracked.
            if (!IsDefaultKeyResolved(invocation, allowKeySize: false))
            {
                Report(context, syntax, NamedDefaultKeySizeMessage);
            }

            return;
        }

        if (AnalyzeStaticCreate(context, invocation, isFramework))
        {
            return;
        }

        if (method.IsStatic)
        {
            return;
        }

        if (invocation.Instance?.Type is not { } instanceType)
        {
            return;
        }

        var family = GetFamily(instanceType);

        // ECDsa.GenerateKey(ECCurve) and ECDiffieHellman.GenerateKey(ECCurve).
        if (family == Family.EllipticCurve && string.Equals(method.Name, "GenerateKey", StringComparison.Ordinal))
        {
            CheckCurveArgument(context, invocation.Arguments, syntax);
        }
        else if (family == Family.EllipticCurve && EcImportMethods.Contains(method.Name))
        {
            Report(context, syntax, ImportedCurveMessage(instanceType.Name));
        }
        else if (family == Family.Rsa && ImportMethods.Contains(method.Name))
        {
            Report(context, syntax, ImportedKeySizeMessage(instanceType.Name));
        }
    }

    /// <summary>
    ///     Reports an <c lang="csharp">RSA.Create</c> method group, such as <c lang="csharp">Func&lt;int, RSA&gt; create = RSA.Create</c>. The
    ///     key size arrives when the delegate runs, so it cannot be verified here.
    /// </summary>
    private static void AnalyzeMethodReference(OperationAnalysisContext context)
    {
        var reference = (IMethodReferenceOperation)context.Operation;
        var method = reference.Method;
        if (method.IsStatic &&
            string.Equals(method.Name, "Create", StringComparison.Ordinal) &&
            FipsSymbolHelpers.IsCryptographyType(method.ContainingType, "RSA"))
        {
            Report(
                context,
                reference.Syntax,
                $"RSA key size cannot be verified at compile time because the {method.ContainingType.Name}.Create method group is invoked later; ensure it is at least {MinRsaBitsText} bits");
        }
    }

    private static void AnalyzeObjectCreation(OperationAnalysisContext context, bool isFramework)
    {
        var creation = (IObjectCreationOperation)context.Operation;
        if (creation.Type is not { } createdType)
        {
            return;
        }

        var family = GetFamily(createdType);
        if (family == Family.None)
        {
            return;
        }

        var syntax = creation.Syntax;
        if (family == Family.EllipticCurve && CheckCurveArgument(context, creation.Arguments, syntax))
        {
            return;
        }

        if (family == Family.Rsa)
        {
            CheckRsaCreation(context, creation, creation.Arguments, createdType.Name, syntax, isFramework);
        }
        else if (TryGetKeySizeArgument(creation.Arguments, out var keySize))
        {
            CheckKeySize(context, family, createdType.Name, keySize, syntax);
        }
        else if (family == Family.EllipticCurve)
        {
            // No curve and no key size: the type creates the default P-256 key, which is below the policy minimum.
            Report(context, syntax, DefaultCurveMessage(createdType.Name));
        }
    }

    private static void AnalyzeAssignment(OperationAnalysisContext context)
    {
        if (!FipsSymbolHelpers.MayAssignMember(context.Operation, "KeySize") &&
            !FipsSymbolHelpers.MayAssignMember(context.Operation, "Curve"))
        {
            return;
        }

        FipsSymbolHelpers.ForEachAssignment(
            context.Operation,
            context,
            static (state, target, value, syntax) => CheckAssignment(state, target, value, syntax));
    }

    private static void CheckAssignment(OperationAnalysisContext context, IOperation target, IOperation value, SyntaxNode syntax)
    {
        // ECParameters.Curve is a field, so a parameters struct can carry a weak curve.
        if (target is IFieldReferenceOperation { Field.Name: "Curve" } curveField &&
            FipsSymbolHelpers.IsCryptographyType(curveField.Field.ContainingType, "ECParameters"))
        {
            CheckCurveValue(context, value, syntax);

            return;
        }

        if (target is not IPropertyReferenceOperation { Property.Name: "KeySize" } property ||
            !FipsSymbolHelpers.IsInCryptographyNamespace(property.Property.ContainingType))
        {
            return;
        }

        if (property.Instance?.Type is not { } instanceType)
        {
            return;
        }

        var family = GetFamily(instanceType);
        if (family == Family.None)
        {
            return;
        }

        CheckKeySize(context, family, instanceType.Name, value, syntax);
    }

    /// <summary>The first int parameter is the key size, such as <c lang="csharp">RSACryptoServiceProvider(int, CspParameters)</c>.</summary>
    private static bool TryGetKeySizeArgument(
        ImmutableArray<IArgumentOperation> arguments,
        [NotNullWhen(true)] out IOperation? keySize)
    {
        foreach (var argument in arguments)
        {
            if (argument.Parameter?.Type.SpecialType == SpecialType.System_Int32)
            {
                keySize = argument.Value;
                return true;
            }
        }

        keySize = null;
        return false;
    }

    /// <summary>
    ///     Checks an RSA factory call or constructor: a key size argument is read, an imported key is reported as
    ///     unverifiable, and any other overload creates a key of the platform default size.
    /// </summary>
    private static void CheckRsaCreation(
        OperationAnalysisContext context,
        IOperation creation,
        ImmutableArray<IArgumentOperation> arguments,
        string typeName,
        SyntaxNode syntax,
        bool isFramework)
    {
        if (TryGetKeySizeArgument(arguments, out var keySize))
        {
            CheckKeySize(context, Family.Rsa, typeName, keySize, syntax);
            return;
        }

        foreach (var argument in arguments)
        {
            var type = argument.Parameter?.Type;
            if (type is null)
            {
                continue;
            }

            if (type.SpecialType == SpecialType.System_IntPtr ||
                (FipsSymbolHelpers.IsInCryptographyNamespace(type) && ImportedKeyTypes.Contains(type.Name)))
            {
                Report(context, syntax, ImportedKeySizeMessage(typeName));
                return;
            }
        }

        // Assigning KeySize does not reliably resize an RSACryptoServiceProvider key, so only an import, which
        // reports for itself, replaces its default report. On .NET Framework RSA.Create() returns that type.
        var isCryptoServiceProvider = FipsSymbolHelpers.DerivesFromCryptographyType(creation.Type, "RSACryptoServiceProvider");
        var isFrameworkFactory = isFramework && creation is IInvocationOperation;
        if (IsDefaultKeyResolved(creation, allowKeySize: !isCryptoServiceProvider && !isFrameworkFactory))
        {
            return;
        }

        var message = isCryptoServiceProvider
            ? $"{typeName} created without an explicit key size uses the platform default, which cannot be verified, and assigning KeySize afterward is not a reliable way to set it; use the constructor that takes a key size of at least {MinRsaBitsText} bits"
            : isFrameworkFactory
                ? $"{typeName} created without an explicit key size uses the platform default, which cannot be verified, and on .NET Framework assigning KeySize afterward is not a reliable way to set it; use an overload that takes a key size of at least {MinRsaBitsText} bits"
                : $"{typeName} created without an explicit key size uses the platform default, which cannot be verified; use an explicit key size of at least {MinRsaBitsText} bits or set KeySize before use";
        Report(context, syntax, message);
    }

    /// <summary>
    ///     Returns true for <c lang="csharp">CryptoConfig.CreateFromName</c>, <c lang="csharp">AsymmetricAlgorithm.Create</c> and
    ///     <c lang="csharp">RSA.Create</c> with a constant name that creates an RSA key.
    /// </summary>
    private static bool IsRsaCreatedByName(IInvocationOperation invocation)
    {
        var method = invocation.TargetMethod;
        if (!method.IsStatic ||
            !((string.Equals(method.Name, "CreateFromName", StringComparison.Ordinal) &&
               FipsSymbolHelpers.IsCryptographyType(method.ContainingType, "CryptoConfig")) ||
              (string.Equals(method.Name, "Create", StringComparison.Ordinal) &&
               (FipsSymbolHelpers.IsCryptographyType(method.ContainingType, "AsymmetricAlgorithm") ||
                FipsSymbolHelpers.IsCryptographyType(method.ContainingType, "RSA")))) ||
            method.Parameters.Length == 0 ||
            method.Parameters[0].Type.SpecialType != SpecialType.System_String ||
            !FipsSymbolHelpers.TryGetConstant<string>(FipsSymbolHelpers.ArgumentAt(invocation.Arguments, 0)?.Value, out var name))
        {
            return false;
        }

        name = name.Trim();

        return string.Equals(name, "RSA", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, FipsSymbolHelpers.Cryptography + ".RSA", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, FipsSymbolHelpers.Cryptography + ".AsymmetricAlgorithm", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, FipsSymbolHelpers.Cryptography + ".RSACryptoServiceProvider", StringComparison.OrdinalIgnoreCase);
    }

    private static string ImportedKeySizeMessage(string typeName) =>
        $"{typeName} key size cannot be verified because the key is imported; policy requires at least {MinRsaBitsText} bits";

    private static string ImportedCurveMessage(string typeName) =>
        $"{typeName} curve cannot be verified because the key is imported; policy requires P-384 or P-521";

    private static string DefaultCurveMessage(string typeName) =>
        $"{typeName} created without a curve argument or a key size uses the default P-256 curve; policy requires P-384 or P-521";

    /// <summary>
    ///     Returns true when the object initializer, or the first statement that uses the local receiving the new
    ///     key, sets <c lang="csharp">KeySize</c> or imports a key. Each of those reports for itself.
    /// </summary>
    private static bool IsDefaultKeyResolved(IOperation creation, bool allowKeySize)
    {
        if (allowKeySize && creation is IObjectCreationOperation objectCreation &&
            FipsSymbolHelpers.InitializerAssigns(objectCreation, "KeySize"))
        {
            return true;
        }

        var use = FipsSymbolHelpers.FindFirstLocalUse(creation, out var block);

        // An upcast local (AsymmetricAlgorithm) does not reach the RSA KeySize or import checks, so it stays unresolved.
        return use is not null &&
            GetFamily(use.Type) == Family.Rsa &&
            ((allowKeySize && FipsSymbolHelpers.IsUnconditionalPropertyAssignment(use, "KeySize", block)) ||
             (use.Parent is IInvocationOperation invocation &&
              invocation.Instance == use &&
              ImportMethods.Contains(invocation.TargetMethod.Name) &&
              invocation.Parent is IExpressionStatementOperation { Parent: { } parent } &&
              parent == block));
    }

    private static void CheckKeySize(
        OperationAnalysisContext context,
        Family family,
        string typeName,
        IOperation value,
        SyntaxNode syntax)
    {
        var minimum = family == Family.EllipticCurve ? MinEllipticCurveBits : MinRsaBits;

        if (FipsSymbolHelpers.TryGetResolvedConstant<int>(value, out var bits))
        {
            if (bits < minimum)
            {
                Report(
                    context,
                    syntax,
                    $"{typeName} key size {bits.ToString(CultureInfo.InvariantCulture)} is below the policy minimum of {minimum.ToString(CultureInfo.InvariantCulture)} bits");
            }
        }
        else
        {
            Report(context, syntax, $"{typeName} key size cannot be verified at compile time; ensure it is at least {minimum.ToString(CultureInfo.InvariantCulture)} bits");
        }
    }

    /// <summary>
    ///     Checks the first <c lang="csharp">ECCurve</c> argument. Returns true when the call has one, so a constructor does not
    ///     also look for a key size.
    /// </summary>
    private static bool CheckCurveArgument(
        OperationAnalysisContext context,
        ImmutableArray<IArgumentOperation> arguments,
        SyntaxNode syntax)
    {
        foreach (var argument in arguments)
        {
            if (!FipsSymbolHelpers.IsCryptographyType(argument.Parameter?.Type, "ECCurve"))
            {
                continue;
            }

            CheckCurveValue(context, argument.Value, syntax);

            return true;
        }

        return false;
    }

    private static void CheckCurveValue(OperationAnalysisContext context, IOperation value, SyntaxNode syntax)
    {
        if (TryGetCurveName(value, out var curve))
        {
            if (!AllowedCurves.Contains(curve))
            {
                Report(context, syntax, $"Curve '{curve}' is not P-384 or P-521; use nistP384 or nistP521");
            }
        }
        else
        {
            // The twin key-size path reports an unresolvable value on purpose, so an unreadable curve cannot hide an unapproved curve.
            Report(context, syntax, "Curve name cannot be verified at compile time; use nistP384 or nistP521");
        }
    }

    private static bool TryGetCurveName(IOperation value, out string curve, int depth = 0)
    {
        curve = string.Empty;
        var operation = FipsSymbolHelpers.Unwrap(value);

        // A curve held in a readonly field or an unreassigned local resolves to its initializer.
        if (operation is IFieldReferenceOperation or ILocalReferenceOperation)
        {
            return depth < 4 &&
                FipsSymbolHelpers.ResolveInitializer(operation) is { } resolved &&
                TryGetCurveName(resolved, out curve, depth + 1);
        }

        // ECCurve.NamedCurves.nistP256
        if (operation is IPropertyReferenceOperation { Property: { IsStatic: true } property } &&
            property.ContainingType is { Name: "NamedCurves", ContainingType: { } outer } &&
            FipsSymbolHelpers.IsCryptographyType(outer, "ECCurve"))
        {
            curve = property.Name;
            return true;
        }

        // ECCurve.CreateFromFriendlyName("nistP256") and ECCurve.CreateFromValue("1.2.840.10045.3.1.7")
        if (operation is IInvocationOperation { TargetMethod: { IsStatic: true } method } invocation &&
            (string.Equals(method.Name, "CreateFromFriendlyName", StringComparison.Ordinal) ||
             string.Equals(method.Name, "CreateFromValue", StringComparison.Ordinal)) &&
            FipsSymbolHelpers.IsCryptographyType(method.ContainingType, "ECCurve") &&
            invocation.Arguments.Length == 1 &&
            FipsSymbolHelpers.TryGetConstant<string>(invocation.Arguments[0].Value, out var text))
        {
            curve = text;
            return true;
        }

        // ECCurve.CreateFromOid(new Oid("1.2.840.10045.3.1.7"))
        if (operation is IInvocationOperation { TargetMethod: { IsStatic: true, Name: "CreateFromOid" } oidMethod } oidInvocation &&
            FipsSymbolHelpers.IsCryptographyType(oidMethod.ContainingType, "ECCurve") &&
            oidInvocation.Arguments.Length == 1 &&
            FipsSymbolHelpers.Unwrap(oidInvocation.Arguments[0].Value) is IObjectCreationOperation oid &&
            FipsSymbolHelpers.IsCryptographyType(oid.Type, "Oid") &&
            FipsSymbolHelpers.ArgumentAt(oid.Arguments, 0)?.Value is { } oidValue &&
            FipsSymbolHelpers.TryGetConstant<string>(oidValue, out var oidText))
        {
            curve = oidText;
            return true;
        }

        return false;
    }

    private enum Family
    {
        None = 0,
        Rsa = 1,
        EllipticCurve = 2,
    }

    private static void Report(OperationAnalysisContext context, SyntaxNode syntax, string message) =>
        context.ReportDiagnostic(Diagnostic.Create(Rule, syntax.GetLocation(), message));
}

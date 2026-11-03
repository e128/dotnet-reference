using System;
using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace E128.Analyzers.Security;

/// <summary>
///     E128145: Reports PBKDF2 calls below the organizational minimum: fewer than 600,000 iterations, an
///     overload that defaults to 1,000 iterations, or SHA-1 as the PRF.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FipsPbkdfIterationAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128145";

    private const int MinIterations = 600_000;

    private static readonly string MinIterationsText = MinIterations.ToString(CultureInfo.InvariantCulture);
    private const string KeyDerivationNamespace = "Microsoft.AspNetCore.Cryptography.KeyDerivation";

    private static readonly ImmutableHashSet<string> WeakHashNames = ImmutableHashSet.Create(
        StringComparer.OrdinalIgnoreCase,
        "SHA1",
        "SHA-1",
        "MD5",
        "SHA",
        "RIPEMD",
        "RIPEMD160",
        "RIPEMD-160");

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Use a strong PBKDF2 configuration",
        "{0}",
        "Security",
        DiagnosticSeverity.Error,
        true,
        "PBKDF2 must use at least 600,000 iterations, and policy prefers a SHA-2 PRF. A value that is not a " +
        "compile-time constant is reported as unverifiable. An IterationCount assignment in an object " +
        "initializer or as the first statement that uses the object replaces the default-count report.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        // Key derivation settings are reported in generated code too, because a generated PBKDF2 call is a real
        // policy defect. FIPS0005 parity requires this opt-in.
        context.ConfigureGeneratedCodeAnalysis(
            GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();

        context.RegisterOperationAction(AnalyzeObjectCreation, OperationKind.ObjectCreation);
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
        context.RegisterOperationAction(
            AnalyzeAssignment,
            OperationKind.SimpleAssignment,
            OperationKind.CoalesceAssignment,
            OperationKind.CompoundAssignment,
            OperationKind.DeconstructionAssignment);
    }

    private static void AnalyzeAssignment(OperationAnalysisContext context)
    {
        if (!FipsSymbolHelpers.MayAssignMember(context.Operation, "IterationCount"))
        {
            return;
        }

        FipsSymbolHelpers.ForEachAssignment(
            context.Operation,
            context,
            static (state, target, value, syntax) => CheckIterationCountAssignment(state, target, value, syntax));
    }

    private static void CheckIterationCountAssignment(
        OperationAnalysisContext context,
        IOperation target,
        IOperation value,
        SyntaxNode syntax)
    {
        if (FipsSymbolHelpers.IsCandidateAssignmentTarget(target, "IterationCount") &&
            target is IPropertyReferenceOperation property &&
            FipsSymbolHelpers.IsCryptographyType(property.Property.ContainingType, "Rfc2898DeriveBytes"))
        {
            CheckIterations(context, value, syntax.GetLocation());
        }
    }

    /// <summary>
    ///     Returns true when the object initializer, or the first statement that uses the local receiving the new
    ///     object, assigns <c lang="csharp">IterationCount</c>. That assignment reports for itself.
    /// </summary>
    private static bool IsIterationCountAssigned(IObjectCreationOperation creation)
    {
        if (FipsSymbolHelpers.InitializerAssigns(creation, "IterationCount"))
        {
            return true;
        }

        var use = FipsSymbolHelpers.FindFirstLocalUse(creation, out var block);

        return use is not null && FipsSymbolHelpers.IsUnconditionalPropertyAssignment(use, "IterationCount", block);
    }

    private static void AnalyzeObjectCreation(OperationAnalysisContext context)
    {
        var creation = (IObjectCreationOperation)context.Operation;
        if (FipsSymbolHelpers.IsCryptographyType(creation.Type, "PbeParameters"))
        {
            CheckPbeParameters(context, creation);
            return;
        }

        if (!FipsSymbolHelpers.DerivesFromCryptographyType(creation.Type, "Rfc2898DeriveBytes"))
        {
            return;
        }

        var location = creation.Syntax.GetLocation();
        var iterations = FipsSymbolHelpers.ArgumentNamed(creation.Arguments, "iterations");
        var hash = FipsSymbolHelpers.ArgumentNamed(creation.Arguments, "hashAlgorithm");

        // A subclass constructor picks its own parameters, so only a named iterations or hashAlgorithm argument
        // is checked. The overload defaults below apply to Rfc2898DeriveBytes itself.
        if (!FipsSymbolHelpers.IsCryptographyType(creation.Type, "Rfc2898DeriveBytes"))
        {
            if (iterations is not null)
            {
                CheckIterations(context, iterations.Value, location);
            }

            if (hash is not null)
            {
                CheckHashAlgorithm(context, hash.Value, location);
            }

            return;
        }

        CheckRfc2898DeriveBytesDefaults(context, creation, iterations, hash);
    }

    /// <summary>
    ///     Checks the argument defaults of <c lang="csharp">new Rfc2898DeriveBytes(...)</c> itself. The overloads
    ///     differ, so an absent iteration count and an absent hash are each checked on their own.
    /// </summary>
    private static void CheckRfc2898DeriveBytesDefaults(
        OperationAnalysisContext context,
        IObjectCreationOperation creation,
        IArgumentOperation? iterations,
        IArgumentOperation? hash)
    {
        var location = creation.Syntax.GetLocation();

        if (iterations is null)
        {
            if (!IsIterationCountAssigned(creation))
            {
                Report(
                    context,
                    location,
                    $"Rfc2898DeriveBytes overload uses the default of 1000 iterations; use an explicit count of at least {MinIterationsText}");
            }
        }
        else
        {
            CheckIterations(context, iterations.Value, location);
        }

        if (hash is null)
        {
            Report(
                context,
                location,
                "Rfc2898DeriveBytes overload uses SHA1 as the PRF by default; pass HashAlgorithmName.SHA256 or stronger");
        }
        else
        {
            CheckHashAlgorithm(context, hash.Value, location);
        }
    }

    /// <summary>
    ///     Checks the PRF hash and the iteration count of <c lang="csharp">new PbeParameters(algorithm, hash, iterations)</c>,
    ///     the password-based encryption settings of a PKCS#8 or PKCS#12 export.
    /// </summary>
    private static void CheckPbeParameters(OperationAnalysisContext context, IObjectCreationOperation creation)
    {
        var location = creation.Syntax.GetLocation();

        if (FipsSymbolHelpers.ArgumentNamed(creation.Arguments, "hashAlgorithm") is { } hash)
        {
            CheckHashAlgorithm(context, hash.Value, location);
        }

        if (FipsSymbolHelpers.ArgumentNamed(creation.Arguments, "iterationCount") is { } iterations)
        {
            CheckIterations(context, iterations.Value, location);
        }
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;
        if (!method.IsStatic || !string.Equals(method.Name, "Pbkdf2", StringComparison.Ordinal))
        {
            return;
        }

        var location = invocation.Syntax.GetLocation();

        if (FipsSymbolHelpers.IsCryptographyType(method.ContainingType, "Rfc2898DeriveBytes"))
        {
            var iterations = FipsSymbolHelpers.ArgumentNamed(invocation.Arguments, "iterations");
            if (iterations is not null)
            {
                CheckIterations(context, iterations.Value, location);
            }

            var hash = FipsSymbolHelpers.ArgumentNamed(invocation.Arguments, "hashAlgorithm");
            if (hash is not null)
            {
                CheckHashAlgorithm(context, hash.Value, location);
            }
        }
        else if (FipsSymbolHelpers.IsType(method.ContainingType, "KeyDerivation", KeyDerivationNamespace))
        {
            var iterations = FipsSymbolHelpers.ArgumentNamed(invocation.Arguments, "iterationCount");
            if (iterations is not null)
            {
                CheckIterations(context, iterations.Value, location);
            }

            var prf = FipsSymbolHelpers.ArgumentNamed(invocation.Arguments, "prf");
            if (prf is not null)
            {
                CheckPrf(context, prf.Value, location);
            }
        }
    }

    private static void CheckIterations(OperationAnalysisContext context, IOperation value, Location location)
    {
        if (FipsSymbolHelpers.TryGetResolvedConstant<int>(value, out var iterations))
        {
            if (iterations < MinIterations)
            {
                Report(context, location, $"PBKDF2 iteration count {iterations.ToString(CultureInfo.InvariantCulture)} is below the minimum of {MinIterationsText}");
            }
        }
        else
        {
            Report(
                context,
                location,
                $"PBKDF2 iteration count cannot be verified at compile time; ensure it is at least {MinIterationsText}");
        }
    }

    private static void CheckHashAlgorithm(OperationAnalysisContext context, IOperation value, Location location)
    {
        if (!FipsSymbolHelpers.TryGetHashAlgorithmName(value, out var name))
        {
            Report(
                context,
                location,
                "PBKDF2 hash algorithm cannot be verified at compile time; ensure it is SHA-256 or stronger");
        }
        else if (WeakHashNames.Contains(name))
        {
            Report(context, location, $"PBKDF2 uses {name} as the PRF; use SHA-256 or stronger");
        }
    }

    private static void CheckPrf(OperationAnalysisContext context, IOperation value, Location location)
    {
        var operation = FipsSymbolHelpers.Unwrap(value);

        // KeyDerivationPrf.HMACSHA1 is 0, HMACSHA256 is 1 and HMACSHA512 is 2.
        if (operation is IFieldReferenceOperation { Field: { } field } &&
            FipsSymbolHelpers.IsType(field.ContainingType, "KeyDerivationPrf", KeyDerivationNamespace))
        {
            if (string.Equals(field.Name, "HMACSHA1", StringComparison.Ordinal))
            {
                Report(context, location, $"PBKDF2 uses {field.Name} as the PRF; use SHA-256 or stronger");
            }

            return;
        }

        if (operation is not null && operation.ConstantValue.HasValue)
        {
            if (operation.ConstantValue.Value is int and 0)
            {
                Report(context, location, "PBKDF2 uses HMACSHA1 as the PRF; use SHA-256 or stronger");
            }

            return;
        }

        Report(
            context,
            location,
            "PBKDF2 PRF cannot be verified at compile time; ensure it is HMACSHA256 or stronger");
    }

    private static void Report(OperationAnalysisContext context, Location location, string message) =>
        context.ReportDiagnostic(Diagnostic.Create(Rule, location, message));
}

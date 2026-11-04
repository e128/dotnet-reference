using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace E128.Analyzers.Security;

/// <summary>
///     E128151: Reports an HMAC key whose compile-time length is shorter than the hash output. The policy
///     minimum is stricter than the minimum in NIST guidance.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FipsHmacKeyLengthAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128151";

    private const string HmacPrefix = "HMAC";

    private static readonly ImmutableHashSet<string> StaticHmacMembers = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "HashData",
        "TryHashData",
        "HashDataAsync");

    private static readonly char[] NameSeparators = ['-', '_'];

    /// <summary>
    ///     The hash output length in bytes for each covered hash, keyed by the name without separators. A hash name
    ///     (<c lang="csharp">SHA-256</c>, <c lang="csharp">SHA3_256</c>) and an HMAC type name without its prefix
    ///     (<c lang="csharp">HMACSHA256</c>) share it.
    /// </summary>
    private static readonly Dictionary<string, int> HashOutputBytes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SHA256"] = 32,
        ["SHA3256"] = 32,
        ["SHA384"] = 48,
        ["SHA3384"] = 48,
        ["SHA512"] = 64,
        ["SHA3512"] = 64,
    };

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Use an HMAC key at least as long as the hash output",
        "{0} key is {1} bytes; policy requires at least {2} bytes (the hash output length)",
        "E128-Security",
        DiagnosticSeverity.Error,
        true,
        "Policy requires an HMAC key at least as long as the hash output: 32 bytes for SHA-256, 48 for " +
        "SHA-384 and 64 for SHA-512. The rule reports only keys whose length it can determine at compile time.");

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
        context.RegisterOperationAction(
            AnalyzeAssignment,
            OperationKind.SimpleAssignment,
            OperationKind.CoalesceAssignment,
            OperationKind.DeconstructionAssignment);
    }

    /// <summary>Returns the hash output length in bytes for an HMAC type, or 0 when the type is not covered.</summary>
    private static int MinimumKeyBytes(ITypeSymbol? type) =>
        FipsSymbolHelpers.IsInCryptographyNamespace(type) &&
        type is INamedTypeSymbol { ContainingType: null } named &&
        named.Name.StartsWith(HmacPrefix, StringComparison.Ordinal)
            ? MinimumKeyBytes(named.Name.Substring(HmacPrefix.Length))
            : 0;

    /// <summary>Returns the hash output length in bytes for a hash algorithm name, or 0 when it is not covered.</summary>
    private static int MinimumKeyBytes(string hashName)
    {
        var normalized = hashName.IndexOfAny(NameSeparators) < 0
            ? hashName
            : hashName.Replace("-", string.Empty).Replace("_", string.Empty);

        return HashOutputBytes.TryGetValue(normalized, out var bytes) ? bytes : 0;
    }

    private static void AnalyzeObjectCreation(OperationAnalysisContext context)
    {
        var creation = (IObjectCreationOperation)context.Operation;
        var minimum = MinimumKeyBytes(creation.Type);
        if (minimum != 0 && creation.Type is { } type)
        {
            CheckKey(context, type.Name, minimum, creation.Arguments, creation.Syntax);
        }
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;
        if (!method.IsStatic)
        {
            return;
        }

        var syntax = invocation.Syntax;

        if (StaticHmacMembers.Contains(method.Name))
        {
            if (method.ContainingType is { } owner)
            {
                var minimum = MinimumKeyBytes(owner);
                if (minimum != 0)
                {
                    CheckKey(context, owner.Name, minimum, invocation.Arguments, syntax);
                }
            }

            return;
        }

        // IncrementalHash.CreateHMAC(HashAlgorithmName, key), CryptographicOperations.HmacData and similar.
        if (method.Name.IndexOf("HMAC", StringComparison.OrdinalIgnoreCase) < 0 ||
            !FipsSymbolHelpers.IsInCryptographyNamespace(method.ContainingType))
        {
            return;
        }

        foreach (var argument in FipsSymbolHelpers.HashNameArguments(invocation.Arguments))
        {
            if (!FipsSymbolHelpers.TryGetHashAlgorithmName(argument.Value, out var hashName))
            {
                continue;
            }

            var minimum = MinimumKeyBytes(hashName);
            if (minimum != 0)
            {
                CheckKey(context, "HMAC-" + hashName, minimum, invocation.Arguments, syntax);
            }

            return;
        }
    }

    private static void AnalyzeAssignment(OperationAnalysisContext context)
    {
        if (!FipsSymbolHelpers.MayAssignMember(context.Operation, "Key"))
        {
            return;
        }

        FipsSymbolHelpers.ForEachAssignment(
            context.Operation,
            context,
            static (state, target, value, syntax) => CheckKeyAssignment(state, target, value, syntax));
    }

    private static void CheckKeyAssignment(OperationAnalysisContext context, IOperation target, IOperation value, SyntaxNode syntax)
    {
        if (!FipsSymbolHelpers.IsCandidateAssignmentTarget(target, "Key") ||
            target is not IPropertyReferenceOperation property)
        {
            return;
        }

        var instanceType = property.Instance?.Type;
        var minimum = MinimumKeyBytes(instanceType);

        // A base-typed local (HMAC h = new HMACSHA256()) takes the concrete type from its initializer.
        if (minimum == 0 &&
            FipsSymbolHelpers.DerivesFromCryptographyType(instanceType, "HMAC") &&
            FipsSymbolHelpers.Unwrap(FipsSymbolHelpers.ResolveInitializer(property.Instance)) is IObjectCreationOperation created)
        {
            instanceType = created.Type;
            minimum = MinimumKeyBytes(instanceType);
        }

        if (minimum != 0 &&
            instanceType is { } type &&
            FipsSymbolHelpers.TryGetConstantByteLength(value, out var keyBytes) &&
            keyBytes < minimum)
        {
            Report(context, syntax, type.Name, keyBytes, minimum);
        }
    }

    private static void CheckKey(
        OperationAnalysisContext context,
        string label,
        int minimum,
        ImmutableArray<IArgumentOperation> arguments,
        SyntaxNode syntax)
    {
        var argument = FipsSymbolHelpers.ArgumentNamed(arguments, "key");
        if (argument is null)
        {
            return;
        }

        if (FipsSymbolHelpers.TryGetConstantByteLength(argument.Value, out var keyBytes) && keyBytes < minimum)
        {
            Report(context, syntax, label, keyBytes, minimum);
        }
    }

    private static void Report(OperationAnalysisContext context, SyntaxNode syntax, string label, int keyBytes, int minimum) =>
        context.ReportDiagnostic(Diagnostic.Create(Rule, syntax.GetLocation(), label, keyBytes, minimum));
}

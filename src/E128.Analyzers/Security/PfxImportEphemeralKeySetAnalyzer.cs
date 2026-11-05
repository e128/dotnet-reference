using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace E128.Analyzers.Security;

/// <summary>
///     E128191: Reports a PFX import whose key storage flags leave out
///     <c lang="csharp">X509KeyStorageFlags.EphemeralKeySet</c>. The import then writes the private key to
///     the machine key store, and the key outlives the process.
/// </summary>
/// <remarks>
///     The rule reads two shapes: an <c lang="csharp">X509Certificate2</c> constructor that takes a
///     <c lang="csharp">fileName</c> argument, and a call to
///     <c lang="csharp">X509CertificateLoader.LoadPkcs12</c> or
///     <c lang="csharp">X509CertificateLoader.LoadPkcs12FromFile</c>. It reports a missing flags argument
///     and a constant flags value with no <c lang="csharp">EphemeralKeySet</c> bit. A non-constant flags
///     expression stays silent, because the rule cannot read its value. Constructor parameters come from
///     the resolved constructor symbol, so a positional call and a named call behave the same.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PfxImportEphemeralKeySetAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128191";

    private const string CertificateMetadataName =
        "System.Security.Cryptography.X509Certificates.X509Certificate2";

    private const string KeyStorageFlagsMetadataName =
        "System.Security.Cryptography.X509Certificates.X509KeyStorageFlags";

    private const string LoaderMetadataName =
        "System.Security.Cryptography.X509Certificates.X509CertificateLoader";

    private const string FileNameParameterName = "fileName";

    private const string EphemeralKeySetFieldName = "EphemeralKeySet";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "PFX import without EphemeralKeySet",
        "'{0}' imports a PFX file with no 'EphemeralKeySet', so the private key is written to the machine key store",
        "E128-Security",
        DiagnosticSeverity.Warning,
        true,
        "A PFX import without X509KeyStorageFlags.EphemeralKeySet writes the private key to the " +
        "machine key store. The key then outlives the process and any other account on the host can " +
        "read it. Pass X509KeyStorageFlags.EphemeralKeySet on the import call.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var certificateTypes = MetadataTypes.Find(context.Compilation, CertificateMetadataName);
        var flagTypes = MetadataTypes.Find(context.Compilation, KeyStorageFlagsMetadataName);

        // Without both types the rule has no symbol to match, so it stays off rather than
        // falling back to a name.
        if (certificateTypes.IsEmpty || flagTypes.IsEmpty)
        {
            return;
        }

        if (GetEphemeralKeySetValue(flagTypes[0]) is not { } ephemeralKeySet)
        {
            return;
        }

        context.RegisterSyntaxNodeAction(
            nodeContext => AnalyzeObjectCreation(nodeContext, certificateTypes, flagTypes, ephemeralKeySet),
            SyntaxKind.ObjectCreationExpression,
            SyntaxKind.ImplicitObjectCreationExpression);

        var loaderTypes = MetadataTypes.Find(context.Compilation, LoaderMetadataName);
        if (loaderTypes.IsEmpty)
        {
            return;
        }

        context.RegisterSyntaxNodeAction(
            nodeContext => AnalyzeInvocation(nodeContext, loaderTypes, flagTypes, ephemeralKeySet),
            SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeObjectCreation(
        SyntaxNodeAnalysisContext context,
        ImmutableArray<INamedTypeSymbol> certificateTypes,
        ImmutableArray<INamedTypeSymbol> flagTypes,
        int ephemeralKeySet)
    {
        if (context.SemanticModel.GetOperation(context.Node, context.CancellationToken)
            is not IObjectCreationOperation creation)
        {
            return;
        }

        var constructor = creation.Constructor;
        if (constructor is null
            || !MetadataTypes.Contains(certificateTypes, constructor.ContainingType)
            || !TakesFileName(constructor))
        {
            return;
        }

        var flagsParameter = FindFlagsParameter(constructor, flagTypes);
        if (flagsParameter is null
            || !OmitsEphemeralKeySet(FindArgument(creation.Arguments, flagsParameter), ephemeralKeySet))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            creation.Syntax.GetLocation(),
            constructor.ContainingType.Name));
    }

    private static void AnalyzeInvocation(
        SyntaxNodeAnalysisContext context,
        ImmutableArray<INamedTypeSymbol> loaderTypes,
        ImmutableArray<INamedTypeSymbol> flagTypes,
        int ephemeralKeySet)
    {
        if (context.SemanticModel.GetOperation(context.Node, context.CancellationToken)
            is not IInvocationOperation invocation)
        {
            return;
        }

        var method = invocation.TargetMethod;
        if (!MetadataTypes.Contains(loaderTypes, method.ContainingType) || !IsPkcs12Loader(method.Name))
        {
            return;
        }

        var flagsParameter = FindFlagsParameter(method, flagTypes);
        if (flagsParameter is null
            || !OmitsEphemeralKeySet(FindArgument(invocation.Arguments, flagsParameter), ephemeralKeySet))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            invocation.Syntax.GetLocation(),
            method.ContainingType.Name + "." + method.Name));
    }

    private static bool IsPkcs12Loader(string methodName)
    {
        return string.Equals(methodName, "LoadPkcs12", StringComparison.Ordinal)
               || string.Equals(methodName, "LoadPkcs12FromFile", StringComparison.Ordinal);
    }

    private static bool TakesFileName(IMethodSymbol constructor)
    {
        foreach (var parameter in constructor.Parameters)
        {
            if (string.Equals(parameter.Name, FileNameParameterName, StringComparison.Ordinal)
                && parameter.Type.SpecialType == SpecialType.System_String)
            {
                return true;
            }
        }

        return false;
    }

    private static IParameterSymbol? FindFlagsParameter(
        IMethodSymbol method,
        ImmutableArray<INamedTypeSymbol> flagTypes)
    {
        foreach (var parameter in method.Parameters)
        {
            if (MetadataTypes.Contains(flagTypes, parameter.Type))
            {
                return parameter;
            }
        }

        return null;
    }

    private static IArgumentOperation? FindArgument(
        ImmutableArray<IArgumentOperation> arguments,
        IParameterSymbol parameter)
    {
        foreach (var argument in arguments)
        {
            if (argument.Parameter is { } candidate && candidate.Ordinal == parameter.Ordinal)
            {
                return argument;
            }
        }

        return null;
    }

    /// <summary>
    ///     Returns true when the call omits <c lang="csharp">EphemeralKeySet</c>. A missing argument and
    ///     a constant value without the bit both qualify. A non-constant value stays silent, because the
    ///     rule cannot read its value.
    /// </summary>
    private static bool OmitsEphemeralKeySet(IArgumentOperation? argument, int ephemeralKeySet)
    {
        if (argument is null)
        {
            return true;
        }

        var constant = argument.Value.ConstantValue;
        return constant.HasValue
               && constant.Value is int flags
               && (flags & ephemeralKeySet) != ephemeralKeySet;
    }

    private static int? GetEphemeralKeySetValue(INamedTypeSymbol flagType)
    {
        foreach (var member in flagType.GetMembers(EphemeralKeySetFieldName))
        {
            if (member is IFieldSymbol { HasConstantValue: true, ConstantValue: int value })
            {
                return value;
            }
        }

        return null;
    }
}

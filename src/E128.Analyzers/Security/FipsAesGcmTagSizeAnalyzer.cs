using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace E128.Analyzers.Security;

/// <summary>
///     E128149: Reports an AES-GCM tag shorter than 16 bytes. The rule reads a constant <c lang="csharp">tagSizeInBytes</c>
///     passed to the <c lang="csharp">AesGcm</c> constructor and the constant size of a tag buffer passed to
///     <c lang="csharp">Encrypt</c> or <c lang="csharp">Decrypt</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FipsAesGcmTagSizeAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128149";

    private const int RequiredTagBytes = 16;

    private const int BitsPerByte = 8;

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Use the full 16-byte AES-GCM tag",
        "AES-GCM tag size {0} bytes ({1} bits) is below the policy minimum of 16 bytes (128 bits)",
        "E128-Security",
        DiagnosticSeverity.Error,
        true,
        "NIST SP 800-38D permits shorter tags in some cases, but policy requires the full 16-byte tag. A " +
        "tag size or tag buffer held in an unreassigned local or a readonly field resolves to its initializer.");

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

    private static void AnalyzeObjectCreation(OperationAnalysisContext context)
    {
        var creation = (IObjectCreationOperation)context.Operation;
        if (!FipsSymbolHelpers.DerivesFromCryptographyType(creation.Type, "AesGcm"))
        {
            return;
        }

        if (FipsSymbolHelpers.ArgumentNamed(creation.Arguments, "tagSizeInBytes") is not { } argument ||
            !FipsSymbolHelpers.TryGetResolvedConstant<int>(argument.Value, out var tagBytes) ||
            tagBytes >= RequiredTagBytes)
        {
            return;
        }

        Report(context, creation, tagBytes);
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;
        if ((!string.Equals(method.Name, "Encrypt", StringComparison.Ordinal) &&
             !string.Equals(method.Name, "Decrypt", StringComparison.Ordinal)) ||
            !FipsSymbolHelpers.DerivesFromCryptographyType(method.ContainingType, "AesGcm"))
        {
            return;
        }

        if (FipsSymbolHelpers.ArgumentNamed(invocation.Arguments, "tag") is not { } argument ||
            !FipsSymbolHelpers.TryGetConstantByteLength(argument.Value, out var tagBytes) ||
            tagBytes >= RequiredTagBytes)
        {
            return;
        }

        Report(context, invocation, tagBytes);
    }

    private static void Report(OperationAnalysisContext context, IOperation operation, int tagBytes)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            operation.Syntax.GetLocation(),
            tagBytes,
            tagBytes * BitsPerByte));
    }
}

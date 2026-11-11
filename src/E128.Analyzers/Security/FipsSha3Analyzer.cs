using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace E128.Analyzers.Security;

/// <summary>
///     E128150: Reports SHA-3, SHAKE, KMAC and HMAC-SHA-3 usage. NIST SP 800-131A Rev. 2 lists SHA3-224
///     through SHA3-512 as acceptable, and FIPS 202 specifies SHAKE. This rule is organizational policy.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FipsSha3Analyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128150";

    private static readonly ImmutableHashSet<string> Sha3Types = FipsSymbolHelpers.Sha3TypeNames;

    private static readonly ImmutableHashSet<string> Sha3AlgorithmNames = FipsSymbolHelpers.Sha3AlgorithmNames;

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Do not use SHA-3, SHAKE or KMAC",
        "'{0}' is blocked by policy; use SHA-256, SHA-384 or SHA-512",
        "E128-Security",
        DiagnosticSeverity.Error,
        true,
        "This organization's policy blocks SHA-3 and SHAKE. The rule is organizational policy, not a " +
        "statement that NIST forbids these algorithms. Reading IsSupported is not reported.");

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
        context.RegisterOperationAction(AnalyzePropertyReference, OperationKind.PropertyReference);
        context.RegisterOperationAction(AnalyzeMethodReference, OperationKind.MethodReference);
        context.RegisterOperationAction(
            AnalyzeHashNameStore,
            OperationKind.SimpleAssignment,
            OperationKind.CoalesceAssignment,
            OperationKind.DeconstructionAssignment,
            OperationKind.FieldInitializer,
            OperationKind.PropertyInitializer);
    }

    private static void AnalyzeObjectCreation(OperationAnalysisContext context)
    {
        var creation = (IObjectCreationOperation)context.Operation;
        var syntax = creation.Syntax;

        if (ReportType(context, creation.Type, syntax))
        {
            return;
        }

        ReportHashNameArguments(context, creation.Constructor, creation.Arguments, syntax);
    }

    private static void AnalyzePropertyReference(OperationAnalysisContext context)
    {
        var reference = (IPropertyReferenceOperation)context.Operation;
        var property = reference.Property;

        // RSAEncryptionPadding.OaepSHA3_256 and its siblings.
        if (!property.IsStatic ||
            !property.Name.StartsWith("OaepSHA3_", StringComparison.Ordinal) ||
            !FipsSymbolHelpers.IsCryptographyType(property.ContainingType, "RSAEncryptionPadding"))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            reference.Syntax.GetLocation(),
            property.Name.Substring("Oaep".Length)));
    }

    private static void AnalyzeMethodReference(OperationAnalysisContext context)
    {
        var reference = (IMethodReferenceOperation)context.Operation;
        if (!reference.Method.IsStatic)
        {
            return;
        }

        _ = ReportType(context, reference.Method.ContainingType, reference.Syntax);
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;
        var syntax = invocation.Syntax;

        // SHA3_256.HashData(...), Shake256.HashData(...) and similar static members.
        if (method.IsStatic && ReportType(context, method.ContainingType, syntax))
        {
            return;
        }

        ReportHashNameArguments(context, method, invocation.Arguments, syntax);
    }

    private static void ReportHashNameArguments(
        OperationAnalysisContext context,
        IMethodSymbol? method,
        ImmutableArray<IArgumentOperation> arguments,
        SyntaxNode syntax)
    {
        FipsSymbolHelpers.ReportHashNameArguments(
            method,
            arguments,
            syntax,
            context,
            static (_, name) => Sha3AlgorithmNames.Contains(name),
            Report);
    }

    private static void AnalyzeHashNameStore(OperationAnalysisContext context)
    {
        FipsSymbolHelpers.ReportHashNameStores(
            context.Operation,
            context,
            static (_, name) => Sha3AlgorithmNames.Contains(name),
            Report);
    }

    private static void Report(OperationAnalysisContext context, SyntaxNode syntax, string name)
    {
        context.ReportDiagnostic(Diagnostic.Create(Rule, syntax.GetLocation(), name));
    }

    private static bool ReportType(OperationAnalysisContext context, ITypeSymbol? type, SyntaxNode syntax)
    {
        if (type is null || !FipsSymbolHelpers.IsCryptographyType(type, Sha3Types))
        {
            return false;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, syntax.GetLocation(), type.Name));

        return true;
    }
}

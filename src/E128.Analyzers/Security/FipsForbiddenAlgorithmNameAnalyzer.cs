using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace E128.Analyzers.Security;

/// <summary>
///     E128148: Reports ciphers identified by type name: Blowfish, RC4, Arcfour, CAST5, CAST6, IDEA and
///     Skipjack. Third-party libraries such as BouncyCastle expose these ciphers. A type that E128071 reports is
///     skipped, so each type gets one diagnostic. The name match can miss a cipher with an unusual name.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FipsForbiddenAlgorithmNameAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128148";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Do not use a cipher excluded by policy",
        "Type '{0}' is blocked by organizational policy because its name matches an excluded cipher (Blowfish, RC4, Arcfour, CAST5, CAST6, IDEA or Skipjack)",
        "E128-Security",
        DiagnosticSeverity.Error,
        true,
        "Third-party libraries such as BouncyCastle expose these ciphers. The organizational policy blocks " +
        "them. The rule matches the type name as a whole word, so it can miss a cipher with an unusual name.");

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
        context.RegisterOperationAction(AnalyzeMethodReference, OperationKind.MethodReference);
    }

    private static void AnalyzeObjectCreation(OperationAnalysisContext context)
    {
        var creation = (IObjectCreationOperation)context.Operation;

        // E128071 covers the policy's named algorithms. This rule adds the name-only cipher matches.
        InspectType(context, creation.Type, creation.Syntax);
    }

    /// <summary>
    ///     Reports a static factory or one-shot helper of a cipher type, such as
    ///     <c lang="csharp">BlowfishEngine.Create()</c> or <c lang="csharp">BlowfishEngine.HashData(...)</c>.
    /// </summary>
    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;
        if (!method.IsStatic || !FipsTypeClassifier.OneShotMembers.Contains(method.Name))
        {
            return;
        }

        InspectType(context, method.ContainingType, invocation.Syntax);
    }

    /// <summary>
    ///     Reports a method group that names a static factory or one-shot helper of a cipher type.
    /// </summary>
    private static void AnalyzeMethodReference(OperationAnalysisContext context)
    {
        var reference = (IMethodReferenceOperation)context.Operation;
        if (!reference.Method.IsStatic || !FipsTypeClassifier.OneShotMembers.Contains(reference.Method.Name))
        {
            return;
        }

        InspectType(context, reference.Method.ContainingType, reference.Syntax);
    }

    private static void InspectType(OperationAnalysisContext context, ITypeSymbol? type, SyntaxNode syntax)
    {
        if (type is null || FipsTypeClassifier.GetVerdict(context.Compilation, type) is not TypeVerdict.ForbiddenName)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, syntax.GetLocation(), FipsSymbolHelpers.FullName(type)));
    }
}

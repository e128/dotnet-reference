using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace E128.Analyzers.Testing;

/// <summary>
///     E128104: Reports an xUnit <c lang="csharp">Assert</c> call whose operands are all compile-time
///     constants, as in <c lang="csharp">Assert.Equal("alpha", "alpha")</c>. Such an assertion passes or
///     fails regardless of the code under test, so it checks nothing.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ConstantOperandAssertionAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128104";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Assertion with literal operands",
        "Every operand of this assertion is a literal, so the assertion cannot depend on the code under test",
        "Testing",
        DiagnosticSeverity.Warning,
        true,
        "An xUnit Assert call whose operands are all literals always passes or always fails. " +
        "Assert a computed value, or a named constant, instead.");

    // A member listed here takes no operand that the code under test can influence. The skip family
    // reports state, and Fail fails unconditionally, so a literal message on either is not a tautology.
    private static readonly ImmutableHashSet<string> NonOperandMembers = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "Skip",
        "SkipWhen",
        "SkipUnless",
        "Fail");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(Analyze, OperationKind.Invocation);
    }

    private static void Analyze(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;

        if (method.ContainingType is not { } containingType
            || !string.Equals(containingType.Name, "Assert", StringComparison.Ordinal)
            || !IsXunitNamespace(containingType.ContainingNamespace)
            || NonOperandMembers.Contains(method.Name))
        {
            return;
        }

        var arguments = invocation.Arguments;

        if (arguments.Length == 0)
        {
            return;
        }

        for (var i = 0; i < arguments.Length; i++)
        {
            // A literal, not merely a constant: comparing two distinct named constants is a
            // deliberate value pin, while a literal-to-literal comparison can never surprise anyone.
            if (arguments[i].Value.Kind != OperationKind.Literal)
            {
                return;
            }
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.Syntax.GetLocation()));
    }

    private static bool IsXunitNamespace(INamespaceSymbol? ns)
    {
        return ns is not null && string.Equals(ns.ToDisplayString(), "Xunit", StringComparison.Ordinal);
    }
}

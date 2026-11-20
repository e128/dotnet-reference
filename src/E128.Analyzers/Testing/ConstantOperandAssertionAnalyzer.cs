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
        "E128-Testing",
        DiagnosticSeverity.Warning,
        true,
        "An xUnit Assert call whose operands are all literals always passes or always fails. " +
        "Assert a computed value, or a named constant, instead.");

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

        if (!XunitTestHelper.IsAssertType(method.ContainingType)
            || NonOperandMembers.Contains(method.Name))
        {
            return;
        }

        var arguments = invocation.Arguments;

        if (arguments.Length is 0)
        {
            return;
        }

        for (var i = 0; i < arguments.Length; i++)
        {
            if (arguments[i].Value.Kind is not OperationKind.Literal)
            {
                return;
            }
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.Syntax.GetLocation()));
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Testing;

/// <summary>
///     E128107 and E128108: Reports an xUnit test method that matches low-value test conditions. One condition
///     reports E128107 at suggestion. Two or more report E128108 at warning. Each condition names a shape that
///     locks the production code in place, so the test fails on every honest refactor.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed partial class LowValueTestAnalyzer : DiagnosticAnalyzer
{
    internal const string SuggestionDiagnosticId = "E128107";

    internal const string WarningDiagnosticId = "E128108";

    private const int BareSizeStatementCount = 3;

    private const int NameMirrorOtherTokenLimit = 1;

    private const int DuplicateKeyThreshold = 2;

    private static readonly ImmutableHashSet<string> UnorderedResultMetadataNames = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "ISet`1",
        "IReadOnlySet`1",
        "IDictionary`2",
        "IReadOnlyDictionary`2",
        "IDictionary");

    private static readonly ImmutableHashSet<string> LoggerTokens = ImmutableHashSet.Create(
        StringComparer.OrdinalIgnoreCase,
        "log",
        "logs",
        "logger",
        "loggers",
        "logging");

    private static readonly DiagnosticDescriptor SuggestionRule = new(
        SuggestionDiagnosticId,
        "Test matches one low-value test condition",
        "Test '{0}' matches the low-value condition {1} and may lock in an implementation detail",
        "E128-Testing",
        DiagnosticSeverity.Info,
        false,
        "A short test that asserts on an implementation detail fails on every honest refactor. " +
        "Delete the test, or assert observable behavior instead.");

    private static readonly DiagnosticDescriptor WarningRule = new(
        WarningDiagnosticId,
        "Test matches several low-value test conditions",
        "Test '{0}' matches the low-value conditions {1} and locks in implementation details",
        "E128-Testing",
        DiagnosticSeverity.Warning,
        false,
        "A short test that asserts on several implementation details fails on every honest refactor. " +
        "Delete the test, or assert observable behavior instead.");

    private const string DuplicateCoverageCondition = "DuplicateCoverage";

    private static readonly ImmutableArray<(string Name, Func<TestFacts, bool> Match)> Detectors =
    [
        (nameof(BareSize), BareSize),
        (nameof(NameMirror), NameMirror),
        (nameof(NoAssertion), NoAssertion),
        (nameof(SingleRowTheory), SingleRowTheory),
        (nameof(LiteralEcho), LiteralEcho),
        (nameof(ExceptionMessageLock), ExceptionMessageLock),
        (nameof(MockVerifyOnly), MockVerifyOnly),
        (nameof(LogAssert), LogAssert),
        (nameof(ConstructorPassthrough), ConstructorPassthrough),
        (nameof(SelfFulfillingExpected), SelfFulfillingExpected),
        (nameof(MagicConstantEcho), MagicConstantEcho),
        (nameof(OrderLock), OrderLock)
    ];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [SuggestionRule, WarningRule];

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
        var entries = new ConcurrentBag<MethodEntry>();

        context.RegisterSyntaxNodeAction(
            nodeContext => AnalyzeMethod(nodeContext, entries),
            SyntaxKind.MethodDeclaration);

        context.RegisterCompilationEndAction(endContext => ReportDiagnostics(endContext, entries));
    }

    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context, ConcurrentBag<MethodEntry> entries)
    {
        var declaration = (MethodDeclarationSyntax)context.Node;

        if (declaration.Body is null && declaration.ExpressionBody is null)
        {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken) is not { } method
            || !XunitTestHelper.IsXunitTestMethod(method))
        {
            return;
        }

        var facts = TestFacts.Create(declaration, method, context.SemanticModel, context.CancellationToken);
        var conditions = ImmutableArray.CreateBuilder<string>();

        foreach (var (name, match) in Detectors)
        {
            if (match(facts))
            {
                conditions.Add(name);
            }
        }

        var duplicateKeys = DuplicateKeys(facts);

        if (conditions.Count is 0 && duplicateKeys.IsEmpty)
        {
            return;
        }

        entries.Add(new MethodEntry(
            declaration.Identifier.GetLocation(),
            method.Name,
            conditions.ToImmutable(),
            duplicateKeys));
    }

    private static void ReportDiagnostics(CompilationAnalysisContext context, ConcurrentBag<MethodEntry> entries)
    {
        var keyCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var entry in entries)
        {
            foreach (var key in entry.DuplicateKeys)
            {
                _ = keyCounts.TryGetValue(key, out var count);
                keyCounts[key] = count + 1;
            }
        }

        foreach (var entry in entries)
        {
            var conditions = entry.Conditions;

            foreach (var key in entry.DuplicateKeys)
            {
                if (keyCounts[key] >= DuplicateKeyThreshold)
                {
                    conditions = conditions.Add(DuplicateCoverageCondition);
                    break;
                }
            }

            if (conditions.IsEmpty)
            {
                continue;
            }

            var rule = conditions.Length >= 2 ? WarningRule : SuggestionRule;
            var names = string.Join(", ", conditions.OrderBy(static name => name, StringComparer.Ordinal));
            context.ReportDiagnostic(Diagnostic.Create(rule, entry.Location, entry.MethodName, names));
        }
    }

    private static ImmutableArray<string> DuplicateKeys(TestFacts facts)
    {
        if (facts.Symbol.ContainingType is not { } testClass
            || facts.Assertions.IsEmpty
            || !HasLiteralProductionCall(facts))
        {
            return [];
        }

        return [$"{testClass.ToDisplayString()}|{BodySignature(facts.Method)}"];
    }

    private static string BodySignature(MethodDeclarationSyntax method)
    {
        var tokens = method.Body is { } body
            ? body.DescendantTokens()
            : method.ExpressionBody?.DescendantTokens() ?? [];

        return string.Join(" ", tokens.Select(static token => token.Text));
    }

    private static bool HasLiteralProductionCall(TestFacts facts)
    {
        foreach (var call in facts.ProductionCalls)
        {
            if (IsAllLiteralArguments(call.Invocation) && IsProductionCodeCall(call.Symbol))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsProductionCodeCall(IMethodSymbol method)
    {
        if (method.ContainingType?.TypeKind is TypeKind.Interface)
        {
            return false;
        }

        var ns = method.ContainingNamespace?.ToDisplayString() ?? string.Empty;

        return !ns.StartsWith("System", StringComparison.Ordinal)
               && !ns.StartsWith("Microsoft", StringComparison.Ordinal);
    }

    private static bool BareSize(TestFacts facts)
    {
        return !IsNullOnly(facts)
               && facts.Statements == BareSizeStatementCount
               && facts.Assertions.Length is 1
               && facts.ProductionCalls.IsEmpty
               && !ReadsProductionMember(facts);
    }

    private static bool ReadsProductionMember(TestFacts facts)
    {
        var testClass = facts.Symbol.ContainingType;

        foreach (var member in facts.Method.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
        {
            var symbol = facts.Model.GetSymbolInfo(member, facts.CancellationToken).Symbol;

            if (symbol is { ContainingType: { } owner }
                && !SymbolEqualityComparer.Default.Equals(owner, testClass)
                && !TestFacts.IsAssertion(symbol))
            {
                return true;
            }
        }

        return false;
    }

    private static bool NameMirror(TestFacts facts)
    {
        string? mirrored = null;

        foreach (var call in facts.ProductionCalls)
        {
            if (mirrored is null)
            {
                mirrored = call.Symbol.Name;
            }
            else if (!string.Equals(mirrored, call.Symbol.Name, StringComparison.Ordinal))
            {
                return false;
            }
        }

        if (mirrored is null)
        {
            return false;
        }

        var mirrors = false;
        var otherTokens = 0;

        foreach (var token in TokenizeName(facts.Method.Identifier.ValueText))
        {
            if (string.Equals(token, mirrored, StringComparison.Ordinal))
            {
                mirrors = true;
            }
            else
            {
                otherTokens++;
            }
        }

        return mirrors && otherTokens <= NameMirrorOtherTokenLimit;
    }

    private static bool NoAssertion(TestFacts facts)
    {
        return !facts.ContainsAwait
               && !facts.HasHelperCall
               && facts.Assertions.Length is 0
               && facts.ProductionCalls.Length > 0
               && !CallsVoidProduction(facts)
               && !CallsCheckStyleAssertion(facts);
    }

    private static bool CallsVoidProduction(TestFacts facts)
    {
        foreach (var call in facts.ProductionCalls)
        {
            if (call.Symbol.ReturnsVoid)
            {
                return true;
            }
        }

        return false;
    }

    private static bool CallsCheckStyleAssertion(TestFacts facts)
    {
        foreach (var call in facts.ProductionCalls)
        {
            if (string.Equals(call.Symbol.Name, "Check", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool SingleRowTheory(TestFacts facts)
    {
        var attribute = XunitTestHelper.GetFactOrTheoryAttribute(facts.Method);

        return attribute is not null
               && IsTheoryName(XunitTestHelper.SimpleName(attribute.Name))
               && XunitTestHelper.CountDataRows(facts.Method) is 1;
    }

    private static bool LiteralEcho(TestFacts facts)
    {
        return EveryAssertionMatches(
            facts,
            assertion => !HasToStringInvocation(assertion)
                          && ComparesLiteralWithLiteralOnlyCall(facts, assertion.Invocation));
    }

    private static bool ExceptionMessageLock(TestFacts facts)
    {
        return EveryAssertionMatches(
            facts,
            assertion => !HasToStringInvocation(assertion)
                          && PinsExceptionMessage(facts, assertion));
    }

    private static bool EveryAssertionMatches(
        TestFacts facts,
        Func<AssertionFact, bool> match)
    {
        var examined = false;

        foreach (var assertion in facts.Assertions)
        {
            if (HasLambdaArgument(assertion.Invocation))
            {
                continue;
            }

            examined = true;

            if (!match(assertion))
            {
                return false;
            }
        }

        return examined;
    }

    private static bool MockVerifyOnly(TestFacts facts)
    {
        if (facts.Assertions.Length is not 1)
        {
            return false;
        }

        var assertion = facts.Assertions[0];

        return IsVerificationName(assertion.Symbol.Name) && HasLambdaArgument(assertion.Invocation);
    }

    private static bool LogAssert(TestFacts facts)
    {
        if (facts.Assertions.Length is not 1)
        {
            return false;
        }

        var assertion = facts.Assertions[0];
        var receiver = ReceiverName(assertion.Invocation);

        return IsVerificationName(assertion.Symbol.Name)
               && !HasLambdaArgument(assertion.Invocation)
               && receiver is not null
               && IsLoggerName(receiver);
    }

    private static bool IsLoggerName(string name)
    {
        foreach (var token in TokenizeName(name))
        {
            if (LoggerTokens.Contains(token)
                || token.EndsWith("Log", StringComparison.Ordinal)
                || token.EndsWith("Logger", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ConstructorPassthrough(TestFacts facts)
    {
        return EveryAssertionMatches(facts, assertion => HasLiteralConstructedRead(facts, assertion));
    }

    private static bool SelfFulfillingExpected(TestFacts facts)
    {
        foreach (var assertion in facts.Assertions)
        {
            if (ComparesSameExpression(assertion))
            {
                return true;
            }
        }

        return false;
    }

}

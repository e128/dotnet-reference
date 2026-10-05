using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace E128.Analyzers.Testing;

/// <summary>
///     E128107 and E128108: Reports an xUnit test method that matches low-value test conditions. One condition
///     reports E128107 at suggestion. Two or more report E128108 at warning. Each condition names a shape that
///     locks the production code in place, so the test fails on every honest refactor.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LowValueTestAnalyzer : DiagnosticAnalyzer
{
    internal const string SuggestionDiagnosticId = "E128107";

    internal const string WarningDiagnosticId = "E128108";

    private const int BareSizeStatementCount = 3;

    private const int NameMirrorOtherTokenLimit = 1;

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

    // Both rules are disabled by default. One condition still flags an ordinary unit test that asserts
    // on an internal member, on an exception message, or on a value the test itself seeded. A project
    // that wants the report opts in with dotnet_diagnostic.E128107.severity, or the sibling id.
    private static readonly DiagnosticDescriptor SuggestionRule = new(
        SuggestionDiagnosticId,
        "Test matches one low-value test condition",
        "Test '{0}' matches the low-value condition {1} and may lock in an implementation detail",
        "Testing",
        DiagnosticSeverity.Info,
        false,
        "A short test that asserts on an implementation detail fails on every honest refactor. " +
        "Delete the test, or assert observable behavior instead.");

    private static readonly DiagnosticDescriptor WarningRule = new(
        WarningDiagnosticId,
        "Test matches several low-value test conditions",
        "Test '{0}' matches the low-value conditions {1} and locks in implementation details",
        "Testing",
        DiagnosticSeverity.Warning,
        false,
        "A short test that asserts on several implementation details fails on every honest refactor. " +
        "Delete the test, or assert observable behavior instead.");

    private const string DuplicateCoverageCondition = "DuplicateCoverage";

    // The condition names travel into the diagnostic message. A report that says only "one low-value
    // condition" leaves the reader to guess which detector fired, so the name is part of the contract.
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
        // DuplicateCoverage needs every test method before it can decide. The collector lives in this
        // closure, so each compilation gets its own state and the tier decision can count the duplicate
        // condition beside the pure ones.
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

        if (conditions.Count == 0 && duplicateKeys.IsEmpty)
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
                keyCounts.TryGetValue(key, out var count);
                keyCounts[key] = count + 1;
            }
        }

        foreach (var entry in entries)
        {
            var conditions = entry.Conditions;

            foreach (var key in entry.DuplicateKeys)
            {
                if (keyCounts[key] >= 2)
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
            var names = string.Join(", ", conditions.OrderBy(name => name, StringComparer.Ordinal));
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

        // Two tests cover the same ground only when their bodies match token for token. A key built from
        // the literal-argument call alone collides tests that differ in their setup or in the method under
        // test. The body signature reads both, so only a copy-paste pair shares a key.
        return [$"{testClass.ToDisplayString()}|{BodySignature(facts.Method)}"];
    }

    private static string BodySignature(MethodDeclarationSyntax method)
    {
        // Tokens carry the code without its trivia, so reindenting or recommenting a body does not change
        // the signature. The method name and its attributes sit outside the body and do not enter the key.
        var tokens = method.Body is { } body
            ? body.DescendantTokens()
            : method.ExpressionBody?.DescendantTokens() ?? [];

        return string.Join(" ", tokens.Select(token => token.Text));
    }

    private static bool HasLiteralProductionCall(TestFacts facts)
    {
        foreach (var call in facts.ProductionCalls)
        {
            // A standard-library call seeds test input, it does not carry the behavior under test. Two
            // tests that seed the same range but exercise different production paths are not duplicates.
            if (IsAllLiteralArguments(call.Invocation) && IsProductionCodeCall(call.Symbol))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsProductionCodeCall(IMethodSymbol method)
    {
        // A call on an interface is a library or seam accessor, not the concrete code under test. A DOM
        // read such as IDocument.QuerySelector("img") carries no behavior, so it must not seed a
        // duplicate key or count as the call an assertion echoes.
        if (method.ContainingType?.TypeKind == TypeKind.Interface)
        {
            return false;
        }

        var ns = method.ContainingNamespace?.ToDisplayString() ?? string.Empty;

        return !ns.StartsWith("System", StringComparison.Ordinal)
               && !ns.StartsWith("Microsoft", StringComparison.Ordinal);
    }

    private static bool BareSize(TestFacts facts)
    {
        // A short test that also calls production observes something real. Only a short test that
        // exercises nothing but its own literals has no boundary to sit on. A property read is not a
        // call, so `Assert.Same(Pool.Shared, Pool.Shared)` reaches production without an invocation.
        return !IsNullOnly(facts)
               && facts.Statements == BareSizeStatementCount
               && facts.Assertions.Length == 1
               && facts.ProductionCalls.IsEmpty
               && !ReadsProductionMember(facts);
    }

    private static bool ReadsProductionMember(TestFacts facts)
    {
        var testClass = facts.Symbol.ContainingType;

        foreach (var member in facts.Method.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
        {
            var symbol = facts.Model.GetSymbolInfo(member, facts.CancellationToken).Symbol;

            // The test class holds fixtures and local helpers. Every other type is the code under test,
            // and a read of it reaches a boundary even when the read is a property rather than a call.
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

        // The name mirrors the call only when it carries the method name and almost nothing else. A
        // name that also states a behavior and a condition describes the test, not the call.
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
        // A thrown assertion reports through the call itself, so a call named Check counts as an
        // assertion even though no Assert member appears in the body. Fluent rule libraries use it.
        // A helper declared in the test class asserts on the caller's behalf, so its body is not read here.
        return !facts.ContainsAwait
               && !facts.HasHelperCall
               && facts.Assertions.Length == 0
               && facts.ProductionCalls.Length > 0
               && !CallsVoidProduction(facts)
               && !CallsCheckStyleAssertion(facts);
    }

    private static bool CallsVoidProduction(TestFacts facts)
    {
        // A void method has no return value to assert on, so the throw is the only observable. A test
        // that calls a void validator and asserts nothing passes when the validator stays silent, which
        // is the contract of the happy path, not a missing assertion.
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
               && IsTheoryName(attribute.Name.ToString())
               && XunitTestHelper.CountDataRows(facts.Method) == 1;
    }

    private static bool LiteralEcho(TestFacts facts)
    {
        return EveryAssertionMatches(
            facts,
            invocation => !HasToStringInvocation(invocation)
                          && ComparesLiteralWithLiteralOnlyCall(facts, invocation));
    }

    private static bool ExceptionMessageLock(TestFacts facts)
    {
        return EveryAssertionMatches(
            facts,
            invocation => !HasToStringInvocation(invocation)
                          && PinsExceptionMessage(facts, invocation));
    }

    private static bool EveryAssertionMatches(
        TestFacts facts,
        Func<InvocationExpressionSyntax, bool> match)
    {
        // One observable assertion gives the test its value. A test that mixes an echo with a real
        // check is not low value, so every examined assertion must match before the condition reports.
        // An exception capture only sets up the assertion that follows it, so it does not compete.
        var examined = false;

        foreach (var assertion in facts.Assertions)
        {
            if (HasLambdaArgument(assertion.Invocation))
            {
                continue;
            }

            examined = true;

            if (!match(assertion.Invocation))
            {
                return false;
            }
        }

        return examined;
    }

    private static bool MockVerifyOnly(TestFacts facts)
    {
        if (facts.Assertions.Length != 1)
        {
            return false;
        }

        var assertion = facts.Assertions[0];

        return IsVerificationName(assertion.Symbol.Name) && HasLambdaArgument(assertion.Invocation);
    }

    private static bool LogAssert(TestFacts facts)
    {
        if (facts.Assertions.Length != 1)
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
        // A substring test accepts dialog and catalog. Match a whole name token instead.
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
        return EveryAssertionMatches(facts, invocation => HasLiteralConstructedRead(facts, invocation));
    }

    private static bool SelfFulfillingExpected(TestFacts facts)
    {
        // A receiver-only comparison cannot tell `ranks["A"]` from `ranks["B"]`, so it reports a real
        // relational invariant as circular. The two operands must match expression for expression.
        foreach (var assertion in facts.Assertions)
        {
            if (ComparesSameExpression(assertion))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ComparesSameExpression(AssertionFact assertion)
    {
        var arguments = assertion.Invocation.ArgumentList.Arguments;

        return string.Equals(assertion.Symbol.Name, "Equal", StringComparison.Ordinal)
               && arguments.Count >= 2
               && SyntaxFactory.AreEquivalent(arguments[0].Expression, arguments[1].Expression);
    }

    private static bool MagicConstantEcho(TestFacts facts)
    {
        return !facts.ProductionCalls.IsEmpty
               && EveryAssertionMatches(facts, invocation => EchoesProductionLiteral(facts, invocation));
    }

    private static bool EchoesProductionLiteral(TestFacts facts, InvocationExpressionSyntax assertion)
    {
        foreach (var node in AssertionNodes(assertion))
        {
            if (node is LiteralExpressionSyntax literal
                && ProductionBodyContainsLiteral(facts, literal.Token.Text))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ProductionBodyContainsLiteral(TestFacts facts, string text)
    {
        foreach (var call in facts.ProductionCalls)
        {
            foreach (var reference in call.Symbol.DeclaringSyntaxReferences)
            {
                if (reference.GetSyntax(facts.CancellationToken) is not MethodDeclarationSyntax production)
                {
                    continue;
                }

                foreach (var node in production.DescendantNodes())
                {
                    if (node is LiteralExpressionSyntax literal
                        && IsReturnedValue(literal)
                        && string.Equals(literal.Token.Text, text, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool IsReturnedValue(SyntaxNode node)
    {
        for (var ancestor = node.Parent; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (ancestor is ReturnStatementSyntax or ArrowExpressionClauseSyntax)
            {
                return true;
            }

            if (ancestor is StatementSyntax)
            {
                return false;
            }
        }

        return false;
    }

    private static bool OrderLock(TestFacts facts)
    {
        foreach (var assertion in facts.Assertions)
        {
            if (PinsExactOrderOnUnorderedResult(facts, assertion))
            {
                return true;
            }
        }

        return false;
    }

    private static bool PinsExactOrderOnUnorderedResult(TestFacts facts, AssertionFact assertion)
    {
        if (!string.Equals(assertion.Symbol.Name, "Equal", StringComparison.Ordinal))
        {
            return false;
        }

        var arguments = assertion.Invocation.ArgumentList.Arguments;

        if (arguments.Count < 2)
        {
            return false;
        }

        var actualType = facts.Model.GetTypeInfo(arguments[1].Expression, facts.CancellationToken).Type;
        return IsSetOrDictionary(actualType);
    }

    private static bool IsSetOrDictionary(ITypeSymbol? type)
    {
        if (type is null)
        {
            return false;
        }

        if (UnorderedResultMetadataNames.Contains(type.OriginalDefinition.MetadataName))
        {
            return true;
        }

        foreach (var iface in type.AllInterfaces)
        {
            if (UnorderedResultMetadataNames.Contains(iface.OriginalDefinition.MetadataName))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasLiteralConstructedRead(TestFacts facts, InvocationExpressionSyntax assertion)
    {
        var nodes = AssertionNodes(assertion);
        var literals = new HashSet<string>(StringComparer.Ordinal);

        foreach (var node in nodes)
        {
            if (node is LiteralExpressionSyntax literal)
            {
                literals.Add(literal.Token.Text);
            }
        }

        if (literals.Count == 0)
        {
            return false;
        }

        // The test echoes a value that it passed into the constructor. A literal that the constructor
        // never received asserts something else, so it does not match.
        foreach (var node in nodes)
        {
            if (node is MemberAccessExpressionSyntax member
                && ConstructedInstance(facts, member) is { } creation
                && CreationCarriesLiteral(creation, literals))
            {
                return true;
            }
        }

        return false;
    }

    private static bool CreationCarriesLiteral(
        ObjectCreationExpressionSyntax creation,
        HashSet<string> literals)
    {
        if (creation.ArgumentList is null)
        {
            return false;
        }

        foreach (var argument in creation.ArgumentList.Arguments)
        {
            if (argument.Expression is LiteralExpressionSyntax literal && literals.Contains(literal.Token.Text))
            {
                return true;
            }
        }

        return false;
    }

    private static ObjectCreationExpressionSyntax? ConstructedInstance(
        TestFacts facts,
        MemberAccessExpressionSyntax member)
    {
        if (facts.Model.GetOperation(member, facts.CancellationToken) is not IPropertyReferenceOperation property)
        {
            return null;
        }

        if (property.Instance is IObjectCreationOperation creation)
        {
            return creation.Syntax as ObjectCreationExpressionSyntax;
        }

        if (property.Instance is not ILocalReferenceOperation local)
        {
            return null;
        }

        foreach (var reference in local.Local.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax(facts.CancellationToken)
                is VariableDeclaratorSyntax { Initializer.Value: ObjectCreationExpressionSyntax fromLocal })
            {
                return fromLocal;
            }
        }

        return null;
    }

    private static bool ComparesLiteralWithLiteralOnlyCall(TestFacts facts, InvocationExpressionSyntax assertion)
    {
        var literals = new HashSet<string>(StringComparer.Ordinal);
        var calls = new List<InvocationExpressionSyntax>();

        foreach (var argument in assertion.ArgumentList.Arguments)
        {
            // The literal that the test echoes sits in its own argument. A literal inside the call
            // under test is an input, not an expected value, so it does not count. A library accessor
            // such as IDocument.QuerySelector("img") is not the call under test and does not echo.
            if (IsLiteralOnlyProductionCall(facts, argument.Expression, out var call))
            {
                calls.Add(call!);
                continue;
            }

            // An argument that invokes production code states a computed value, not an echoed literal.
            // `Assert.Equal(Hash((object)"x"), Hash("x"))` compares two calls, and the literal is only
            // the shared input.
            if (ContainsInvocation(facts, argument.Expression))
            {
                continue;
            }

            foreach (var node in argument.Expression.DescendantNodesAndSelf(IsNotLambda))
            {
                if (node is LiteralExpressionSyntax literal)
                {
                    literals.Add(literal.Token.Text);
                }
            }
        }

        if (literals.Count == 0)
        {
            return false;
        }

        // An echo repeats an input back as the expected value, so `Assert.Equal("abc", Normalize("abc"))`
        // has no behavior between the input and the assertion. A literal that differs from every input,
        // such as `Assert.Equal("user_domain.com", SanitizeKey("user@domain.com"))`, states a result the
        // code computed. That is a real assertion, not an echo.
        foreach (var call in calls)
        {
            if (CallCarriesLiteral(call, literals))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsInvocation(TestFacts facts, ExpressionSyntax expression)
    {
        foreach (var node in expression.DescendantNodesAndSelf(IsNotLambda))
        {
            if (node is InvocationExpressionSyntax invocation
                && facts.Model.GetSymbolInfo(invocation, facts.CancellationToken).Symbol is IMethodSymbol)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsLiteralOnlyProductionCall(
        TestFacts facts,
        ExpressionSyntax expression,
        out InvocationExpressionSyntax? call)
    {
        call = expression as InvocationExpressionSyntax;

        return call is not null
               && IsAllLiteralArguments(call)
               && facts.Model.GetSymbolInfo(call, facts.CancellationToken).Symbol is IMethodSymbol called
               && IsProductionCodeCall(called);
    }

    private static bool CallCarriesLiteral(InvocationExpressionSyntax call, HashSet<string> literals)
    {
        foreach (var argument in call.ArgumentList.Arguments)
        {
            if (argument.Expression is LiteralExpressionSyntax literal && literals.Contains(literal.Token.Text))
            {
                return true;
            }
        }

        return false;
    }

    private static bool PinsExceptionMessage(TestFacts facts, InvocationExpressionSyntax assertion)
    {
        // Only an exact-equality pin locks the whole message. A substring check with Assert.Contains
        // pins a documented hint, which is the contract, so it does not report.
        if (assertion.Expression is not MemberAccessExpressionSyntax access
            || !string.Equals(access.Name.Identifier.ValueText, "Equal", StringComparison.Ordinal))
        {
            return false;
        }

        var hasLiteral = false;
        var hasExceptionMessage = false;

        foreach (var node in AssertionNodes(assertion))
        {
            if (node is LiteralExpressionSyntax)
            {
                hasLiteral = true;
            }
            else if (node is MemberAccessExpressionSyntax member
                     && string.Equals(member.Name.Identifier.ValueText, "Message", StringComparison.Ordinal)
                     && DerivesFromException(facts.Model.GetTypeInfo(member.Expression, facts.CancellationToken).Type))
            {
                hasExceptionMessage = true;
            }
        }

        return hasLiteral && hasExceptionMessage;
    }

    private static bool HasToStringInvocation(InvocationExpressionSyntax assertion)
    {
        foreach (var node in AssertionNodes(assertion))
        {
            if (node is InvocationExpressionSyntax call
                && call.Expression is MemberAccessExpressionSyntax member
                && string.Equals(member.Name.Identifier.ValueText, "ToString", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static List<SyntaxNode> AssertionNodes(InvocationExpressionSyntax assertion)
    {
        var nodes = new List<SyntaxNode>();

        foreach (var argument in assertion.ArgumentList.Arguments)
        {
            nodes.AddRange(argument.Expression.DescendantNodesAndSelf(IsNotLambda));
        }

        return nodes;
    }

    private static bool IsNotLambda(SyntaxNode node)
    {
        return node is not (LambdaExpressionSyntax or AnonymousMethodExpressionSyntax);
    }

    private static bool IsAllLiteralArguments(InvocationExpressionSyntax call)
    {
        if (call.ArgumentList.Arguments.Count == 0)
        {
            return false;
        }

        foreach (var argument in call.ArgumentList.Arguments)
        {
            if (argument.Expression is not LiteralExpressionSyntax)
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasLambdaArgument(InvocationExpressionSyntax assertion)
    {
        foreach (var argument in assertion.ArgumentList.Arguments)
        {
            if (argument.Expression is LambdaExpressionSyntax or AnonymousMethodExpressionSyntax)
            {
                return true;
            }
        }

        return false;
    }

    private static string? ReceiverName(InvocationExpressionSyntax assertion)
    {
        return assertion.Expression switch
        {
            MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax receiver } => receiver.Identifier.ValueText,
            MemberAccessExpressionSyntax { Expression: MemberAccessExpressionSyntax receiver } =>
                receiver.Name.Identifier.ValueText,
            _ => null
        };
    }

    private static bool IsVerificationName(string name)
    {
        return name.StartsWith("Verify", StringComparison.Ordinal);
    }

    private static bool DerivesFromException(ITypeSymbol? type)
    {
        for (var current = type as INamedTypeSymbol; current is not null; current = current.BaseType)
        {
            if (string.Equals(current.ToDisplayString(), "System.Exception", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsNullOnly(TestFacts facts)
    {
        if (facts.Assertions.Length != 1)
        {
            return false;
        }

        var name = facts.Assertions[0].Symbol.Name;
        return string.Equals(name, "NotNull", StringComparison.Ordinal)
               || string.Equals(name, "Null", StringComparison.Ordinal);
    }

    private static bool IsTheoryName(string name)
    {
        return string.Equals(name, "Theory", StringComparison.Ordinal)
               || string.Equals(name, "TheoryAttribute", StringComparison.Ordinal);
    }

    private static ImmutableArray<string> TokenizeName(string name)
    {
        var tokens = ImmutableArray.CreateBuilder<string>();
        var start = 0;

        for (var i = 1; i <= name.Length; i++)
        {
            if (i != name.Length && name[i] != '_' && !char.IsUpper(name[i]))
            {
                continue;
            }

            var token = name.Substring(start, i - start).Trim('_');
            if (token.Length > 0)
            {
                tokens.Add(token);
            }

            start = i;
        }

        return tokens.ToImmutable();
    }
}

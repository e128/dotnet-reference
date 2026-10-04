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

    private static readonly DiagnosticDescriptor SuggestionRule = new(
        SuggestionDiagnosticId,
        "Test matches one low-value test condition",
        "Test '{0}' matches one low-value test condition and may lock in an implementation detail",
        "Testing",
        DiagnosticSeverity.Info,
        true,
        "A short test that asserts on an implementation detail fails on every honest refactor. " +
        "Delete the test, or assert observable behavior instead.");

    private static readonly DiagnosticDescriptor WarningRule = new(
        WarningDiagnosticId,
        "Test matches several low-value test conditions",
        "Test '{0}' matches several low-value test conditions and locks in implementation details",
        "Testing",
        DiagnosticSeverity.Warning,
        true,
        "A short test that asserts on several implementation details fails on every honest refactor. " +
        "Delete the test, or assert observable behavior instead.");

    private static readonly ImmutableArray<Func<TestFacts, bool>> Detectors =
    [
        BareSize,
        NameMirror,
        NoAssertion,
        SingleRowTheory,
        LiteralEcho,
        ExceptionMessageLock,
        MockVerifyOnly,
        LogAssert,
        ConstructorPassthrough,
        InternalsReachIn,
        SelfFulfillingExpected,
        MagicConstantEcho,
        OrderLock
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
        var hitCount = 0;

        foreach (var detector in Detectors)
        {
            if (detector(facts))
            {
                hitCount++;
            }
        }

        var duplicateKeys = DuplicateKeys(facts);

        if (hitCount == 0 && duplicateKeys.IsEmpty)
        {
            return;
        }

        entries.Add(new MethodEntry(declaration.Identifier.GetLocation(), method.Name, hitCount, duplicateKeys));
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
            var hitCount = entry.HitCount;

            foreach (var key in entry.DuplicateKeys)
            {
                if (keyCounts[key] >= 2)
                {
                    hitCount++;
                    break;
                }
            }

            if (hitCount == 0)
            {
                continue;
            }

            var rule = hitCount >= 2 ? WarningRule : SuggestionRule;
            context.ReportDiagnostic(Diagnostic.Create(rule, entry.Location, entry.MethodName));
        }
    }

    private static ImmutableArray<string> DuplicateKeys(TestFacts facts)
    {
        if (facts.Symbol.ContainingType is not { } testClass || facts.Assertions.IsEmpty)
        {
            return [];
        }

        if (!HasLiteralProductionCall(facts, out var calls))
        {
            return [];
        }

        var assertions = string.Join(
            "|",
            facts.Assertions.Select(assertion => assertion.Symbol.Name).OrderBy(name => name, StringComparer.Ordinal));
        var builder = ImmutableArray.CreateBuilder<string>();

        // A method that repeats the same call twice covers the same ground twice, but that is not the
        // cross-method duplication this condition counts. Keep one key per distinct call site.
        foreach (var call in calls)
        {
            var arguments = string.Join(
                ",",
                call.Invocation.ArgumentList.Arguments.Select(argument => argument.Expression.ToString()));
            var key = $"{testClass.ToDisplayString()}|{call.Symbol.ToDisplayString()}|{arguments}|{assertions}";

            if (!builder.Contains(key))
            {
                builder.Add(key);
            }
        }

        return builder.ToImmutable();
    }

    private static bool HasLiteralProductionCall(TestFacts facts, out ImmutableArray<ProductionCallFact> calls)
    {
        var builder = ImmutableArray.CreateBuilder<ProductionCallFact>();

        foreach (var call in facts.ProductionCalls)
        {
            if (IsAllLiteralArguments(call.Invocation))
            {
                builder.Add(call);
            }
        }

        calls = builder.ToImmutable();
        return !calls.IsEmpty;
    }

    private static bool BareSize(TestFacts facts)
    {
        // A short test that also calls production observes something real. Only a short test that
        // exercises nothing but its own literals has no boundary to sit on.
        return !IsNullOnly(facts)
               && facts.Statements == BareSizeStatementCount
               && facts.Assertions.Length == 1
               && facts.ProductionCalls.IsEmpty;
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
        return !facts.ContainsAwait
               && facts.Assertions.Length == 0
               && facts.ProductionCalls.Length > 0
               && !CallsCheckStyleAssertion(facts);
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
        foreach (var assertion in facts.Assertions)
        {
            if (!HasToStringInvocation(assertion.Invocation)
                && ComparesLiteralWithLiteralOnlyCall(assertion.Invocation))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ExceptionMessageLock(TestFacts facts)
    {
        foreach (var assertion in facts.Assertions)
        {
            if (!HasToStringInvocation(assertion.Invocation)
                && PinsExceptionMessage(facts, assertion.Invocation))
            {
                return true;
            }
        }

        return false;
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
        foreach (var assertion in facts.Assertions)
        {
            if (HasLiteralConstructedRead(facts, assertion.Invocation))
            {
                return true;
            }
        }

        return false;
    }

    private static bool InternalsReachIn(TestFacts facts)
    {
        foreach (var assertion in facts.Assertions)
        {
            foreach (var node in AssertionNodes(assertion.Invocation))
            {
                if (node is MemberAccessExpressionSyntax member && ReadsInternalMember(facts, member))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool SelfFulfillingExpected(TestFacts facts)
    {
        foreach (var assertion in facts.Assertions)
        {
            if (SharesExpectedReceiver(facts, assertion))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MagicConstantEcho(TestFacts facts)
    {
        if (facts.ProductionCalls.IsEmpty)
        {
            return false;
        }

        foreach (var assertion in facts.Assertions)
        {
            foreach (var node in AssertionNodes(assertion.Invocation))
            {
                if (node is LiteralExpressionSyntax literal
                    && ProductionBodyContainsLiteral(facts, literal.Token.Text))
                {
                    return true;
                }
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

    private static bool ReadsInternalMember(TestFacts facts, MemberAccessExpressionSyntax member)
    {
        var symbol = facts.Model.GetSymbolInfo(member, facts.CancellationToken).Symbol;

        return symbol is IPropertySymbol or IFieldSymbol
               && symbol.DeclaredAccessibility == Accessibility.Internal
               && symbol.ContainingAssembly is { } assembly
               && GrantsInternalsTo(assembly, facts.Symbol.ContainingAssembly.Name);
    }

    private static bool GrantsInternalsTo(IAssemblySymbol assembly, string friendAssemblyName)
    {
        foreach (var attribute in assembly.GetAttributes())
        {
            if (attribute.AttributeClass is not { } attributeClass
                || !string.Equals(attributeClass.Name, "InternalsVisibleToAttribute", StringComparison.Ordinal)
                || !string.Equals(
                    attributeClass.ContainingNamespace.ToDisplayString(),
                    "System.Runtime.CompilerServices",
                    StringComparison.Ordinal))
            {
                continue;
            }

            // The friend name must be the test assembly. Access granted to another assembly does not
            // make this internal member reachable from the test.
            if (attribute.ConstructorArguments.Length > 0
                && attribute.ConstructorArguments[0].Value is string target
                && string.Equals(FriendAssemblyName(target), friendAssemblyName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string FriendAssemblyName(string target)
    {
        // The attribute value can carry the public key after the assembly name.
        var comma = target.IndexOf(',');
        return comma < 0 ? target.Trim() : target.Substring(0, comma).Trim();
    }

    private static bool SharesExpectedReceiver(TestFacts facts, AssertionFact assertion)
    {
        var arguments = assertion.Invocation.ArgumentList.Arguments;

        if (!string.Equals(assertion.Symbol.Name, "Equal", StringComparison.Ordinal) || arguments.Count < 2)
        {
            return false;
        }

        var expected = ReceiverSymbol(facts.Model.GetOperation(arguments[0].Expression, facts.CancellationToken));
        var actual = ReceiverSymbol(facts.Model.GetOperation(arguments[1].Expression, facts.CancellationToken));

        return expected is not null && SymbolEqualityComparer.Default.Equals(expected, actual);
    }

    private static ISymbol? ReceiverSymbol(IOperation? operation)
    {
        return operation switch
        {
            IInvocationOperation invocation => ReceiverSymbol(invocation.Instance),
            IPropertyReferenceOperation property => ReceiverSymbol(property.Instance),
            IFieldReferenceOperation field => ReceiverSymbol(field.Instance),
            IArrayElementReferenceOperation element => ReceiverSymbol(element.ArrayReference),
            IConversionOperation conversion => ReceiverSymbol(conversion.Operand),
            ILocalReferenceOperation local => local.Local,
            IParameterReferenceOperation parameter => parameter.Parameter,
            _ => null
        };
    }

    private static bool ComparesLiteralWithLiteralOnlyCall(InvocationExpressionSyntax assertion)
    {
        var hasLiteral = false;
        var hasEchoedCall = false;

        foreach (var argument in assertion.ArgumentList.Arguments)
        {
            // The literal that the test echoes sits in its own argument. A literal inside the call
            // under test is an input, not an expected value, so it does not count.
            if (argument.Expression is InvocationExpressionSyntax call && IsAllLiteralArguments(call))
            {
                hasEchoedCall = true;
                continue;
            }

            foreach (var node in argument.Expression.DescendantNodesAndSelf(IsNotLambda))
            {
                if (node is LiteralExpressionSyntax)
                {
                    hasLiteral = true;
                    break;
                }
            }
        }

        return hasLiteral && hasEchoedCall;
    }

    private static bool PinsExceptionMessage(TestFacts facts, InvocationExpressionSyntax assertion)
    {
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

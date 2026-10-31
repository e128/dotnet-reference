using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace E128.Analyzers.Testing;

// The assertion echo detectors. Each one reads a test assertion against the production call that
// produced the value, and reports a test that only restates what the production code already says.
public sealed partial class LowValueTestAnalyzer
{
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
               && EveryAssertionMatches(facts, assertion => EchoesProductionLiteral(facts, assertion));
    }

    private static bool EchoesProductionLiteral(TestFacts facts, AssertionFact assertion)
    {
        foreach (var node in assertion.Nodes)
        {
            if (node is LiteralExpressionSyntax literal
                && facts.ProductionReturnsLiteral(literal.Token.Text))
            {
                return true;
            }
        }

        return false;
    }

    // Walk each production body once and collect every literal it returns. The echo detector
    // queries the set per assertion literal.
    internal static ImmutableHashSet<string> CollectReturnedProductionLiterals(TestFacts facts)
    {
        var literals = ImmutableHashSet.CreateBuilder(StringComparer.Ordinal);

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
                    if (node is LiteralExpressionSyntax literal && IsReturnedValue(literal))
                    {
                        literals.Add(literal.Token.Text);
                    }
                }
            }
        }

        return literals.ToImmutable();
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

    private static bool HasLiteralConstructedRead(TestFacts facts, AssertionFact assertion)
    {
        var nodes = assertion.Nodes;
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
                calls.Add(call);
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
        // such as `Assert.Equal("user_example.com", SanitizeKey("user@example.com"))`, states a result the
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
        [NotNullWhen(true)] out InvocationExpressionSyntax? call)
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

    private static bool PinsExceptionMessage(TestFacts facts, AssertionFact assertion)
    {
        // Only an exact-equality pin locks the whole message. A substring check with Assert.Contains
        // pins a documented hint, which is the contract, so it does not report.
        if (assertion.Invocation.Expression is not MemberAccessExpressionSyntax access
            || !string.Equals(access.Name.Identifier.ValueText, "Equal", StringComparison.Ordinal))
        {
            return false;
        }

        var hasLiteral = false;
        var hasExceptionMessage = false;

        foreach (var node in assertion.Nodes)
        {
            if (node is LiteralExpressionSyntax)
            {
                hasLiteral = true;
            }
            else if (node is MemberAccessExpressionSyntax member
                     && string.Equals(member.Name.Identifier.ValueText, "Message", StringComparison.Ordinal)
                     && DerivesFromException(
                         facts.Model.GetTypeInfo(member.Expression, facts.CancellationToken).Type,
                         facts.Model.Compilation.GetTypeByMetadataName("System.Exception")))
            {
                hasExceptionMessage = true;
            }
        }

        return hasLiteral && hasExceptionMessage;
    }

    private static bool HasToStringInvocation(AssertionFact assertion)
    {
        foreach (var node in assertion.Nodes)
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

    internal static ImmutableArray<SyntaxNode> CollectAssertionNodes(InvocationExpressionSyntax assertion)
    {
        var nodes = ImmutableArray.CreateBuilder<SyntaxNode>();

        foreach (var argument in assertion.ArgumentList.Arguments)
        {
            nodes.AddRange(argument.Expression.DescendantNodesAndSelf(IsNotLambda));
        }

        return nodes.ToImmutable();
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

    private static bool DerivesFromException(ITypeSymbol? type, ITypeSymbol? exceptionType)
    {
        return exceptionType is not null && TypeSymbols.DerivesFrom(type, exceptionType);
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

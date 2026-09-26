using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Testing;

/// <summary>
///     E128105: Reports an xUnit <c lang="csharp">[Fact]</c> or <c lang="csharp">[Theory]</c> method that calls a
///     member named in the <c lang="csharp">e128_out_of_repo_resolvers</c> analyzer-config option and carries no
///     skip guard. Such a test resolves a resource that lives outside the repository, so it fails on every
///     machine that lacks the resource.
/// </summary>
/// <remarks>
///     The option holds comma-separated entries. An entry is <c lang="csharp">Type.Member</c> for one member, or
///     a bare <c lang="csharp">Type</c> for every member of that type. With no entry configured the rule stays
///     silent, so a repo that has not opted in sees no diagnostics.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class OutOfRepoResolverAssertionAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128105";

    private const string ResolversOptionKey = "e128_out_of_repo_resolvers";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Test method calls an out-of-repo resolver without a skip guard",
        "Test method '{0}' calls out-of-repo resolver '{1}' without a skip guard",
        "Testing",
        DiagnosticSeverity.Warning,
        true,
        "A test that resolves a resource outside the repository fails wherever that resource is absent. " +
        "Guard the test with Assert.Skip, Assert.SkipWhen, or Assert.SkipUnless before the resolver call, " +
        "or set Skip on the fact or theory attribute.");

    // The three xUnit v3 dynamic-skip entry points. Any of them ahead of the resolver call is a guard.
    private static readonly ImmutableHashSet<string> SkipMembers = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "Skip",
        "SkipWhen",
        "SkipUnless");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.MethodDeclaration);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var declaration = (MethodDeclarationSyntax)context.Node;

        var body = (SyntaxNode?)declaration.Body ?? declaration.ExpressionBody;
        if (body is null)
        {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken) is not { } method
            || !IsXunitTestMethod(method)
            || HasSkipArgument(method))
        {
            return;
        }

        var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(declaration.SyntaxTree);
        if (!options.TryGetValue(ResolversOptionKey, out var rawValue) || string.IsNullOrWhiteSpace(rawValue))
        {
            return;
        }

        var allowlist = ParseAllowlist(rawValue);
        var semanticModel = context.SemanticModel;

        var guarded = false;
        var resolverName = string.Empty;

        // DescendantNodes is a pre-order walk, so a node always precedes its own descendants. An invocation
        // therefore never arrives before a call that encloses it, which is all the ordering this needs.
        foreach (var invocation in body.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (semanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol called)
            {
                continue;
            }

            if (IsSkipCall(called))
            {
                guarded = guarded || resolverName.Length == 0;
                continue;
            }

            if (resolverName.Length == 0 && TryMatchResolver(called, allowlist, out var matched))
            {
                resolverName = matched;
            }
        }

        if (resolverName.Length == 0 || guarded)
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(Rule, declaration.Identifier.GetLocation(), method.Name, resolverName));
    }

    // A bare "Type" entry matches every member of that type, a "Type.Member" entry matches one member.
    // The two lookups share one set: a bare entry never equals a composed "Type.Member" string, and a
    // composed entry never equals a bare type name.
    private static bool TryMatchResolver(IMethodSymbol method, ImmutableHashSet<string> allowlist, out string matched)
    {
        matched = string.Empty;

        if (method.ContainingType is not { } containingType)
        {
            return false;
        }

        if (allowlist.Contains(containingType.Name))
        {
            matched = containingType.Name;
            return true;
        }

        var memberName = containingType.Name + "." + method.Name;
        if (allowlist.Contains(memberName))
        {
            matched = memberName;
            return true;
        }

        return false;
    }

    private static ImmutableHashSet<string> ParseAllowlist(string rawValue)
    {
        var entries = rawValue
            .Split(',')
            .Select(entry => entry.Trim())
            .Where(entry => entry.Length > 0);

        return ImmutableHashSet.CreateRange(StringComparer.Ordinal, entries);
    }

    private static bool IsSkipCall(IMethodSymbol method)
    {
        return SkipMembers.Contains(method.Name)
               && method.ContainingType is { } containingType
               && string.Equals(containingType.Name, "Assert", StringComparison.Ordinal)
               && IsXunitNamespace(containingType.ContainingNamespace);
    }

    private static bool IsXunitTestMethod(IMethodSymbol method)
    {
        foreach (var attribute in method.GetAttributes())
        {
            if (IsFactOrTheoryAttribute(attribute))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasSkipArgument(IMethodSymbol method)
    {
        foreach (var attribute in method.GetAttributes())
        {
            if (!IsFactOrTheoryAttribute(attribute))
            {
                continue;
            }

            foreach (var namedArgument in attribute.NamedArguments)
            {
                // xUnit v3 exposes Skip, SkipType, SkipUnless, and SkipWhen as settable attribute properties.
                // Every one of them makes the test skip conditionally, so every one is a guard.
                if (namedArgument.Key is { } name && name.StartsWith("Skip", StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsFactOrTheoryAttribute(AttributeData attribute)
    {
        return attribute.AttributeClass is { } attributeClass
               && (string.Equals(attributeClass.Name, "FactAttribute", StringComparison.Ordinal)
                   || string.Equals(attributeClass.Name, "TheoryAttribute", StringComparison.Ordinal))
               && IsXunitNamespace(attributeClass.ContainingNamespace);
    }

    private static bool IsXunitNamespace(INamespaceSymbol? ns)
    {
        return ns is not null && string.Equals(ns.ToDisplayString(), "Xunit", StringComparison.Ordinal);
    }
}

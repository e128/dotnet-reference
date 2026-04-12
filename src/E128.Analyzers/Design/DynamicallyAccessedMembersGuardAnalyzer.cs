using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Design;

/// <summary>
/// E128049: Flags usage of <c>[DynamicallyAccessedMembers]</c>.
/// The attribute is correct only for types registered via <c>AddHttpClient&lt;T&gt;()</c>
/// or JSON-serialized discriminated union nested types. Everywhere else it masks real bugs.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DynamicallyAccessedMembersGuardAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128049";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Avoid [DynamicallyAccessedMembers] — suppress with justification if required for HttpClientFactory or JSON discriminated union",
        messageFormat: "Avoid [DynamicallyAccessedMembers] — suppress with justification if required for HttpClientFactory or JSON discriminated union",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description:
            "[DynamicallyAccessedMembers] is correct only for types registered via AddHttpClient<T>() " +
            "(HttpClientFactory reflectively activates them) or JSON-serialized discriminated union nested types " +
            "(System.Text.Json reflection). Everywhere else it is cargo-cult that suppresses MA0182 while " +
            "masking the real bug: a type with no callers. If this use is valid, suppress with " +
            "#pragma warning disable E128049 // Valid: <reason>.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeAttribute, SyntaxKind.Attribute);
    }

    private static void AnalyzeAttribute(SyntaxNodeAnalysisContext context)
    {
        var attribute = (AttributeSyntax)context.Node;
        var name = attribute.Name.ToString();

        if (!string.Equals(name, "DynamicallyAccessedMembers", StringComparison.Ordinal) &&
            !string.Equals(name, "DynamicallyAccessedMembersAttribute", StringComparison.Ordinal))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, attribute.GetLocation()));
    }
}

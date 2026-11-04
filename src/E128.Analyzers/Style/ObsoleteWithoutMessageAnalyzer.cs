using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Style;

/// <summary>
///     E128169: Flags <c lang="csharp">[Obsolete]</c> with no message. A caller needs the replacement named in the warning.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ObsoleteWithoutMessageAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128169";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Explain every Obsolete attribute",
        "[Obsolete] has no message: name the replacement so a caller knows what to use",
        "E128-Style",
        DiagnosticSeverity.Warning,
        true,
        "An Obsolete attribute with no message tells a caller to stop but not where to go. Pass a message that names the replacement.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

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
        var obsoleteType = context.Compilation.GetTypeByMetadataName("System.ObsoleteAttribute");

        if (obsoleteType is null)
        {
            return;
        }

        context.RegisterSyntaxNodeAction(ctx => AnalyzeAttribute(ctx, obsoleteType), SyntaxKind.Attribute);
    }

    private static void AnalyzeAttribute(SyntaxNodeAnalysisContext context, INamedTypeSymbol obsoleteType)
    {
        var attribute = (AttributeSyntax)context.Node;

        if (context.SemanticModel.GetSymbolInfo(attribute, context.CancellationToken).Symbol is not IMethodSymbol constructor
            || !SymbolEqualityComparer.Default.Equals(constructor.ContainingType, obsoleteType))
        {
            return;
        }

        if (!HasMessage(attribute, context))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, attribute.GetLocation()));
        }
    }

    private static bool HasMessage(AttributeSyntax attribute, SyntaxNodeAnalysisContext context)
    {
        if (attribute.ArgumentList is null)
        {
            return false;
        }

        var arguments = attribute.ArgumentList.Arguments;

        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];

            // The message is the first positional argument, or the argument named "message" in any order.
            var isMessage = argument.NameEquals is null
                            && (argument.NameColon is { } nameColon
                                ? string.Equals(nameColon.Name.Identifier.ValueText, "message", StringComparison.Ordinal)
                                : index == 0);

            if (!isMessage)
            {
                continue;
            }

            var constant = context.SemanticModel.GetConstantValue(argument.Expression, context.CancellationToken);

            // A null or blank constant names no replacement. A non-constant expression counts as a message.
            return !constant.HasValue || (constant.Value is string text && !string.IsNullOrWhiteSpace(text));
        }

        return false;
    }
}

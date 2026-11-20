using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Style;

/// <summary>
///     E128063: Flags private static members with a mid-name underscore — e.g.,
///     <c lang="csharp">Nots_supportedExtensions</c>, <c lang="csharp">Creates_enrichmentJsonOptions</c>, <c lang="csharp">Spectres_terminal</c>.
///     These patterns are artifacts of IDE1006 batch-rename operations that mangle identifiers
///     by inserting an underscore at the word boundary instead of adjusting capitalization.
///     <para>
///         Excluded patterns:
///         <list type="bullet">
///             <item>Leading underscore prefix: <c lang="csharp">_foo</c></item>
///             <item>Hungarian prefix: <c lang="csharp">s_foo</c>, <c lang="csharp">m_foo</c>, <c lang="csharp">t_foo</c></item>
///             <item>Double-underscore operators: <c lang="csharp">op_Addition</c>, <c lang="csharp">__foo</c></item>
///             <item>Const fields (SC0219 / IDE0051 already cover const naming)</item>
///         </list>
///     </para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MidNameUnderscoreAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128063";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Private static member name contains a mid-name underscore",
        "Private static member '{0}' contains a mid-name underscore — likely an IDE1006 batch-rename artifact; remove the underscore and adjust capitalization",
        "E128-Style",
        DiagnosticSeverity.Error,
        true,
        "An underscore in the middle of a private static member name (e.g., 'Nots_supportedExtensions') " +
        "is typically an artifact of IDE1006 batch-rename operations that mangle the identifier. " +
        "Leading-underscore prefixes (_foo, s_foo) are excluded.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.Field, SymbolKind.Method, SymbolKind.Property);
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context)
    {
        var symbol = context.Symbol;

        if (PartialMembers.IsImplementationPart(symbol))
        {
            return;
        }

        if (symbol.IsImplicitlyDeclared)
        {
            return;
        }

        if (symbol is IMethodSymbol method && method.MethodKind is MethodKind.PropertyGet or MethodKind.PropertySet)
        {
            return;
        }

        if (!IsPrivateStatic(symbol))
        {
            return;
        }

        if (symbol is IFieldSymbol field && field.IsConst)
        {
            return;
        }

        var name = symbol.Name;
        if (!HasMidNameUnderscore(name))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, symbol.Locations[0], name));
    }

    private static bool IsPrivateStatic(ISymbol symbol)
    {
        if (!symbol.IsStatic)
        {
            return false;
        }

        return symbol.DeclaredAccessibility is Accessibility.Private
            or Accessibility.Internal
            or Accessibility.NotApplicable; // default for class members is private
    }

    /// <summary>
    ///     Returns true when the name contains an underscore at a position that indicates
    ///     a mid-name mangling rather than a legitimate prefix pattern.
    /// </summary>
    internal static bool HasMidNameUnderscore(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        if (name.StartsWith("__", StringComparison.Ordinal))
        {
            return false;
        }

        if (name.StartsWith("op_", StringComparison.Ordinal))
        {
            return false;
        }

        var underscoreIndex = name.IndexOf('_');
        if (underscoreIndex < 0)
        {
            return false;
        }

        if (underscoreIndex is 0)
        {
            return false;
        }

        if (underscoreIndex is not 1)
        {
            return true;
        }

        var nextUnderscore = name.IndexOf('_', underscoreIndex + 1);
        return nextUnderscore > 0;
    }
}

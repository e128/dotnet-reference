using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Style;

/// <summary>
///     E128063: Flags private static members with a mid-name underscore — e.g.,
///     <c>Nots_supportedExtensions</c>, <c>Creates_enrichmentJsonOptions</c>, <c>Spectres_terminal</c>.
///     These patterns are artifacts of IDE1006 batch-rename operations that mangle identifiers
///     by inserting an underscore at the word boundary instead of adjusting capitalization.
///     <para>
///         Excluded patterns:
///         <list type="bullet">
///             <item>Leading underscore prefix: <c>_foo</c></item>
///             <item>Hungarian prefix: <c>s_foo</c>, <c>m_foo</c>, <c>t_foo</c></item>
///             <item>Double-underscore operators: <c>op_Addition</c>, <c>__foo</c></item>
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
        "Style",
        DiagnosticSeverity.Warning,
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

        // Skip compiler-generated symbols (property getter/setter methods like get_Config_timeout)
        if (symbol.IsImplicitlyDeclared)
        {
            return;
        }

        // Skip property accessors — they are reported via the property symbol itself
        if (symbol is IMethodSymbol method && method.MethodKind is MethodKind.PropertyGet or MethodKind.PropertySet)
        {
            return;
        }

        if (!IsPrivateStatic(symbol))
        {
            return;
        }

        // Skip const fields — const naming is covered by other rules
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

        // Exclude double-underscore patterns (operator overloads, compiler-generated)
        if (name.StartsWith("__", StringComparison.Ordinal))
        {
            return false;
        }

        // Exclude operator method names: op_Addition, op_Subtraction, etc.
        if (name.StartsWith("op_", StringComparison.Ordinal))
        {
            return false;
        }

        // Find the position of the first underscore
        var underscoreIndex = name.IndexOf('_');
        if (underscoreIndex < 0)
        {
            return false;
        }

        // Only one underscore at position 0: _foo — leading underscore prefix, OK
        if (underscoreIndex == 0)
        {
            return false;
        }

        // Single-char prefix followed by underscore: s_foo, m_foo, t_foo — Hungarian prefix, OK
        if (underscoreIndex == 1)
        {
            // But if there's another underscore later, that's still a violation:
            // s_foo_bar has a mid-name underscore after the prefix
            var nextUnderscore = name.IndexOf('_', underscoreIndex + 1);
            return nextUnderscore > 0;
        }

        // Underscore at index >= 2: mid-name underscore, violation
        return true;
    }
}

using System;
using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Design;

/// <summary>
///     E128046: Reports classes whose user-defined inheritance depth meets or exceeds a configurable
///     threshold (default 3). Encourages composition over deep inheritance hierarchies.
/// </summary>
/// <remarks>
///     No code fix is provided — fixing excessive inheritance depth requires structural redesign
///     (e.g., extracting interfaces, using composition) that cannot be safely automated.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ExcessiveInheritanceAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128046";

    private const int DefaultThreshold = 3;
    private const string ThresholdOptionKey = "e128_max_inheritance_depth";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Class has excessive user-defined inheritance depth",
        "Class '{0}' has {1} user-defined inheritance level(s), meeting or exceeding the threshold of {2}. Prefer composition over deep inheritance.",
        "Design",
        DiagnosticSeverity.Warning,
        true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSymbolAction(AnalyzeType, SymbolKind.NamedType);
    }

    private static void AnalyzeType(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        // Only analyze classes — skip structs, interfaces, enums, delegates, record structs.
        if (type.TypeKind != TypeKind.Class)
        {
            return;
        }

        var depth = CountUserDefinedDepth(type, context.Compilation);
        if (depth == 0)
        {
            return;
        }

        var threshold = GetThreshold(context, type);
        if (depth < threshold)
        {
            return;
        }

        var location = type.Locations.Length > 0 ? type.Locations[0] : Location.None;
        context.ReportDiagnostic(
            Diagnostic.Create(Rule, location, type.Name, depth, threshold));
    }

    private static int CountUserDefinedDepth(INamedTypeSymbol type, Compilation compilation)
    {
        var depth = 0;
        var current = type.BaseType;

        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            if (IsUserDefined(current, compilation))
            {
                depth++;
            }

            current = current.BaseType;
        }

        return depth;
    }

    // A type is user-defined if it belongs to the current compilation's assembly
    // (not a reference assembly such as System.Runtime or System.IO).
    private static bool IsUserDefined(INamedTypeSymbol type, Compilation compilation)
    {
        return SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, compilation.Assembly);
    }

    private static int GetThreshold(in SymbolAnalysisContext context, INamedTypeSymbol type)
    {
        var syntaxRef = type.DeclaringSyntaxReferences.IsEmpty
            ? null
            : type.DeclaringSyntaxReferences[0];

        if (syntaxRef is null)
        {
            return DefaultThreshold;
        }

        var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(syntaxRef.SyntaxTree);

        return options.TryGetValue(ThresholdOptionKey, out var rawValue)
               && int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var threshold)
               && threshold > 0
            ? threshold
            : DefaultThreshold;
    }
}

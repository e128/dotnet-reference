using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace E128.Analyzers.Design;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ByteSizeForDataSizeAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128080";

    private static readonly ImmutableArray<string> DataSizeSuffixes =
    [
        "Bytes", "Kb", "Kilobytes", "Mb", "Megabytes",
        "Gb", "Gigabytes", "Tb", "Terabytes",
        "FileSize", "MaxSize", "CacheSize", "BufferSize", "ChunkSize"
    ];

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Use ByteSize for data-size values to avoid unit ambiguity",
        "'{0}' is numeric but its name suggests a data size. Use ByteSize instead to eliminate unit ambiguity at call sites.",
        "E128-Design",
        DiagnosticSeverity.Error,
        true,
        "Numeric properties, parameters, and fields whose names imply a data size (e.g., MaxSizeBytes, " +
        "CacheSizeMb) are ambiguous at call sites — callers can't tell the unit from the type alone. " +
        "Use ByteSize to make the unit explicit in the type system.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.Property, SymbolKind.Parameter, SymbolKind.Field);
        context.RegisterOperationAction(AnalyzeVariableDeclarator, OperationKind.VariableDeclarator);
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context)
    {
        var (type, name, location) = context.Symbol switch
        {
            IPropertySymbol p => (p.Type, p.Name, FirstLocation(p)),
            IParameterSymbol p => (p.Type, p.Name, FirstLocation(p)),
            IFieldSymbol f => (f.Type, f.Name, FirstLocation(f)),
            _ => default
        };

        if (type is null || location is null)
        {
            return;
        }

        var effectiveType = TypeSymbols.UnwrapNullable(type);

        if (!IsDataSizeCandidate(effectiveType, context.Symbol.ContainingType, name))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, location, name));
    }

    private static Location? FirstLocation(ISymbol symbol)
    {
        return symbol.Locations.IsEmpty ? null : symbol.Locations[0];
    }

    private static bool IsDataSizeCandidate(ITypeSymbol type, ITypeSymbol? containingType, string name)
    {
        return IsNumericType(type)
            && !ByteSizeType.Is(type)
            && !ByteSizeType.Is(containingType)
            && NameSuffixes.Matches(name, DataSizeSuffixes);
    }

    private static void AnalyzeVariableDeclarator(OperationAnalysisContext context)
    {
        if (context.Operation is not IVariableDeclaratorOperation { Symbol: ILocalSymbol local })
        {
            return;
        }

        var type = TypeSymbols.UnwrapNullable(local.Type);

        if (!IsDataSizeCandidate(type, local.ContainingType, local.Name))
        {
            return;
        }

        var location = local.Locations.IsEmpty ? null : local.Locations[0];
        if (location is null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, location, local.Name));
    }

    private static bool IsNumericType(ITypeSymbol type)
    {
        return type.SpecialType is SpecialType.System_Int32
            or SpecialType.System_Int64
            or SpecialType.System_Single
            or SpecialType.System_Double;
    }
}

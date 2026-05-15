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
        "Design",
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
            IPropertySymbol p => (p.Type, p.Name, p.Locations.IsEmpty ? null : p.Locations[0]),
            IParameterSymbol p => (p.Type, p.Name, p.Locations.IsEmpty ? null : p.Locations[0]),
            IFieldSymbol f => (f.Type, f.Name, f.Locations.IsEmpty ? null : f.Locations[0]),
            _ => default
        };

        if (type is null || location is null)
        {
            return;
        }

        var effectiveType = UnwrapNullable(type);

        if (!IsNumericType(effectiveType))
        {
            return;
        }

        if (IsByteSizeType(effectiveType))
        {
            return;
        }

        if (IsByteSizeType(context.Symbol.ContainingType))
        {
            return;
        }

        if (!HasDataSizeSuffix(name))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, location, name));
    }

    private static void AnalyzeVariableDeclarator(OperationAnalysisContext context)
    {
        if (context.Operation is not IVariableDeclaratorOperation { Symbol: ILocalSymbol local })
        {
            return;
        }

        var type = UnwrapNullable(local.Type);

        if (!IsNumericType(type) || IsByteSizeType(type))
        {
            return;
        }

        if (IsByteSizeType(local.ContainingType))
        {
            return;
        }

        if (!HasDataSizeSuffix(local.Name))
        {
            return;
        }

        var location = local.Locations.IsEmpty ? null : local.Locations[0];
        if (location is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, location, local.Name));
        }
    }

    private static ITypeSymbol UnwrapNullable(ITypeSymbol type)
    {
        return type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } named
               && named.TypeArguments.Length == 1
            ? named.TypeArguments[0]
            : type;
    }

    private static bool IsNumericType(ITypeSymbol type)
    {
        return type.SpecialType is SpecialType.System_Int32
            or SpecialType.System_Int64
            or SpecialType.System_Single
            or SpecialType.System_Double;
    }

    private static bool IsByteSizeType(ITypeSymbol? type)
    {
        return type is not null
               && string.Equals(type.Name, "ByteSize", StringComparison.Ordinal)
               && string.Equals(type.ContainingNamespace?.ToString(), "Pug.Core.Classes", StringComparison.Ordinal);
    }

    private static bool HasDataSizeSuffix(string name)
    {
        foreach (var suffix in DataSizeSuffixes)
        {
            if (name.Length < suffix.Length)
            {
                continue;
            }

            var startIndex = name.Length - suffix.Length;

            if (!char.IsUpper(name[startIndex]))
            {
                continue;
            }

            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

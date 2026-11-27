using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Unsafe;

/// <summary>
///     E128261: A byte-wise marshal copy moves a struct that declares explicit padding.
///     The copy treats the padding bytes as data, so a later read sees stale bytes.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PaddedStructMarshalCopyAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128261";

    private const string MemoryMarshalMetadataName = "System.Runtime.InteropServices.MemoryMarshal";

    private const string UnsafeMetadataName = "System.Runtime.CompilerServices.Unsafe";

    private const string BufferMetadataName = "System.Buffer";

    private const string MarshalMetadataName = "System.Runtime.InteropServices.Marshal";

    private const string StructLayoutMetadataName = "System.Runtime.InteropServices.StructLayoutAttribute";

    private const string FieldOffsetMetadataName = "System.Runtime.InteropServices.FieldOffsetAttribute";

    private const string LayoutKindMetadataName = "System.Runtime.InteropServices.LayoutKind";

    private const string AsBytesName = "AsBytes";

    private const string CastName = "Cast";

    private const string AsName = "As";

    private const string CopyName = "Copy";

    private const string BlockCopyName = "BlockCopy";

    private const string StructureToPtrName = "StructureToPtr";

    private const int SizeOfByte = 1;

    private const int SizeOfInt16 = 2;

    private const int SizeOfInt32 = 4;

    private const int SizeOfInt64 = 8;

    private static readonly ImmutableDictionary<SpecialType, int> SpecialTypeSizes =
        new Dictionary<SpecialType, int>
        {
            [SpecialType.System_Byte] = SizeOfByte,
            [SpecialType.System_SByte] = SizeOfByte,
            [SpecialType.System_Char] = SizeOfInt16,
            [SpecialType.System_Int16] = SizeOfInt16,
            [SpecialType.System_UInt16] = SizeOfInt16,
            [SpecialType.System_Int32] = SizeOfInt32,
            [SpecialType.System_UInt32] = SizeOfInt32,
            [SpecialType.System_Single] = SizeOfInt32,
            [SpecialType.System_Int64] = SizeOfInt64,
            [SpecialType.System_UInt64] = SizeOfInt64,
            [SpecialType.System_Double] = SizeOfInt64,
        }.ToImmutableDictionary();

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Byte-wise access of a padded struct",
        "Byte-wise access of '{0}' exposes {1} padding byte(s) as data: write the fields or remove the layout gap",
        "E128-Unsafe",
        DiagnosticSeverity.Warning,
        true,
        "A byte-wise access reads or moves every byte of the struct, including explicit layout padding. A later read of a field sees the padding as data. Write the fields one by one, or remove the layout gap.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static start =>
        {
            var types = new MarshalTypes(
                start.Compilation.GetTypeByMetadataName(MemoryMarshalMetadataName),
                start.Compilation.GetTypeByMetadataName(UnsafeMetadataName),
                start.Compilation.GetTypeByMetadataName(BufferMetadataName),
                start.Compilation.GetTypeByMetadataName(MarshalMetadataName),
                start.Compilation.GetTypeByMetadataName(StructLayoutMetadataName),
                start.Compilation.GetTypeByMetadataName(FieldOffsetMetadataName),
                start.Compilation.GetTypeByMetadataName(LayoutKindMetadataName));

            if (!types.CanAnalyze())
            {
                return;
            }

            start.RegisterSyntaxNodeAction(node => Analyze(node, types), SyntaxKind.InvocationExpression);
        });
    }

    private static void Analyze(SyntaxNodeAnalysisContext context, MarshalTypes types)
    {
        if (context.Node is not InvocationExpressionSyntax invocation
            || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || GetSourceType(invocation, method, context, types) is not { TypeKind: TypeKind.Struct } source
            || ComputePadding(source, types) is not { } padding
            || padding <= 0)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), source.Name, padding));
    }

    private static INamedTypeSymbol? GetSourceType(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        SyntaxNodeAnalysisContext context,
        MarshalTypes types)
    {
        var containing = method.ContainingType;

        return IsType(containing, types.MemoryMarshal) && IsOneOf(method.Name, AsBytesName, CastName)
            ? FirstTypeArgument(method)
            : IsType(containing, types.UnsafeType) && IsOneOf(method.Name, AsName, CopyName)
                ? FirstTypeArgument(method)
                : IsType(containing, types.Buffer) && string.Equals(method.Name, BlockCopyName, StringComparison.Ordinal)
                    ? FirstArrayElement(invocation, context)
                    : IsType(containing, types.Marshal) && string.Equals(method.Name, StructureToPtrName, StringComparison.Ordinal)
                        ? FirstArgumentType(invocation, context)
                        : null;
    }

    private static bool IsOneOf(string name, string first, string second)
    {
        return string.Equals(name, first, StringComparison.Ordinal)
               || string.Equals(name, second, StringComparison.Ordinal);
    }

    private static INamedTypeSymbol? FirstTypeArgument(IMethodSymbol method)
    {
        return method.TypeArguments.Length > 0 ? method.TypeArguments[0] as INamedTypeSymbol : null;
    }

    private static INamedTypeSymbol? FirstArrayElement(
        InvocationExpressionSyntax invocation,
        SyntaxNodeAnalysisContext context)
    {
        return invocation.ArgumentList.Arguments.Count > 0
               && context.SemanticModel
                   .GetTypeInfo(invocation.ArgumentList.Arguments[0].Expression, context.CancellationToken)
                   .Type is IArrayTypeSymbol array
            ? array.ElementType as INamedTypeSymbol
            : null;
    }

    private static INamedTypeSymbol? FirstArgumentType(
        InvocationExpressionSyntax invocation,
        SyntaxNodeAnalysisContext context)
    {
        return invocation.ArgumentList.Arguments.Count > 0
            ? context.SemanticModel
                .GetTypeInfo(invocation.ArgumentList.Arguments[0].Expression, context.CancellationToken)
                .Type as INamedTypeSymbol
            : null;
    }

    private static int? ComputePadding(INamedTypeSymbol type, MarshalTypes types)
    {
        if (!TryGetExplicitLayout(type, types, out var declaredSize)
            || !TryGetFieldSpans(type, types, out var spans))
        {
            return null;
        }

        return spans.Count is 0 ? null : Padding(spans, declaredSize);
    }

    private static bool TryGetExplicitLayout(
        INamedTypeSymbol type,
        MarshalTypes types,
        out int? declaredSize)
    {
        declaredSize = null;
        var layout = FindAttribute(type, types.StructLayout);

        if (layout is not { ConstructorArguments.Length: 1 }
            || layout.ConstructorArguments[0].Value is not int kind
            || !IsExplicitLayout(kind, types.LayoutKind))
        {
            return false;
        }

        declaredSize = DeclaredSize(layout);
        return true;
    }

    private static bool TryGetFieldSpans(
        INamedTypeSymbol type,
        MarshalTypes types,
        out List<FieldSpan> spans)
    {
        spans = [];

        foreach (var member in type.GetMembers())
        {
            if (member is not IFieldSymbol field || field.IsStatic || field.IsConst || field.IsImplicitlyDeclared)
            {
                continue;
            }

            if (FieldOffset(field, types.FieldOffsetType) is not { } offset
                || FieldSize(field.Type) is not { } size)
            {
                return false;
            }

            spans.Add(new FieldSpan(offset, size));
        }

        return true;
    }

    private static int Padding(List<FieldSpan> spans, int? declaredSize)
    {
        spans.Sort(static (left, right) => left.Offset.CompareTo(right.Offset));

        var end = 0;
        var padding = 0;

        foreach (var span in spans)
        {
            if (span.Offset > end)
            {
                padding += span.Offset - end;
            }

            end = Math.Max(end, span.Offset + span.Size);
        }

        if (declaredSize is { } size && size > end)
        {
            padding += size - end;
        }

        return padding;
    }

    private static int? DeclaredSize(AttributeData layout)
    {
        foreach (var argument in layout.NamedArguments)
        {
            if (string.Equals(argument.Key, "Size", StringComparison.Ordinal) && argument.Value.Value is int size)
            {
                return size;
            }
        }

        return null;
    }

    private static bool IsExplicitLayout(int kind, INamedTypeSymbol? layoutKind)
    {
        if (layoutKind is null)
        {
            return false;
        }

        var members = layoutKind.GetMembers("Explicit");

        return members.Length > 0
               && members[0] is IFieldSymbol field
               && field.HasConstantValue
               && field.ConstantValue is int value
               && value == kind;
    }

    private static int? FieldOffset(IFieldSymbol field, INamedTypeSymbol? offsetAttribute)
    {
        var attribute = FindAttribute(field, offsetAttribute);

        return attribute is { ConstructorArguments.Length: 1 }
               && attribute.ConstructorArguments[0].Value is int offset
            ? offset
            : null;
    }

    private static int? FieldSize(ITypeSymbol type)
    {
        if (type.TypeKind is TypeKind.Enum && type is INamedTypeSymbol { EnumUnderlyingType: { } underlying })
        {
            return FieldSize(underlying);
        }

        return type is IPointerTypeSymbol ? null : SpecialTypeSize(type.SpecialType);
    }

    private static int? SpecialTypeSize(SpecialType type)
    {
        return SpecialTypeSizes.TryGetValue(type, out var size) ? size : null;
    }

    private static AttributeData? FindAttribute(ISymbol symbol, INamedTypeSymbol? attributeType)
    {
        if (attributeType is null)
        {
            return null;
        }

        foreach (var attribute in symbol.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeType))
            {
                return attribute;
            }
        }

        return null;
    }

    private static bool IsType(INamedTypeSymbol? actual, INamedTypeSymbol? expected)
    {
        return expected is not null && SymbolEqualityComparer.Default.Equals(actual, expected);
    }

    private sealed class FieldSpan
    {
        public FieldSpan(int offset, int size)
        {
            Offset = offset;
            Size = size;
        }

        public int Offset { get; }

        public int Size { get; }
    }

    private sealed class MarshalTypes
    {
        public MarshalTypes(
            INamedTypeSymbol? memoryMarshal,
            INamedTypeSymbol? unsafeType,
            INamedTypeSymbol? buffer,
            INamedTypeSymbol? marshal,
            INamedTypeSymbol? structLayout,
            INamedTypeSymbol? fieldOffset,
            INamedTypeSymbol? layoutKind)
        {
            MemoryMarshal = memoryMarshal;
            UnsafeType = unsafeType;
            Buffer = buffer;
            Marshal = marshal;
            StructLayout = structLayout;
            FieldOffsetType = fieldOffset;
            LayoutKind = layoutKind;
        }

        public INamedTypeSymbol? MemoryMarshal { get; }

        public INamedTypeSymbol? UnsafeType { get; }

        public INamedTypeSymbol? Buffer { get; }

        public INamedTypeSymbol? Marshal { get; }

        public INamedTypeSymbol? StructLayout { get; }

        public INamedTypeSymbol? FieldOffsetType { get; }

        public INamedTypeSymbol? LayoutKind { get; }

        public bool CanAnalyze()
        {
            return StructLayout is not null
                   && FieldOffsetType is not null
                   && LayoutKind is not null
                   && (MemoryMarshal is not null || UnsafeType is not null || Buffer is not null || Marshal is not null);
        }
    }
}

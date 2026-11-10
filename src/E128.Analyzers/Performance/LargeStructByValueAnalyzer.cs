using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LargeStructByValueAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128217";

    // Above this size a copy costs more than an indirection, so 'in' pays off.
    private const int MaxByValueSize = 24;

    // The size model assumes a 64-bit process, which is the only target the repo supports.
    private const int PointerSize = 8;

    // A nested struct chain deeper than this is treated as too large to measure. The cap
    // also stops a self-referential struct in broken code from recursing without end.
    private const int MaxDepth = 16;

    // Sizes in bytes of the primitive types and the nullable tag.
    private const int ByteSize = 1;
    private const int ShortSize = 2;
    private const int IntSize = 4;
    private const int LongSize = 8;
    private const int DecimalSize = 16;
    private const int NullableTagSize = 4;

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Large struct passed by value",
        "Parameter '{0}' passes '{1}' (at least {2} bytes) by value — use 'in' or a reference type",
        "E128-Performance",
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

        context.RegisterCompilationStartAction(static startContext =>
        {
            var inlineArray = startContext.Compilation.GetTypeByMetadataName(
                "System.Runtime.CompilerServices.InlineArrayAttribute");

            startContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeParameter(nodeContext, inlineArray),
                SyntaxKind.Parameter);
        });
    }

    private static void AnalyzeParameter(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol? inlineArray)
    {
        var parameter = (ParameterSyntax)context.Node;
        if (parameter.Type is null || HasByReferenceModifier(parameter))
        {
            return;
        }

        var type = context.SemanticModel.GetTypeInfo(parameter.Type, context.CancellationToken).Type;
        if (type is null)
        {
            return;
        }

        if (GetMeasurableStruct(type) is not { } named)
        {
            return;
        }

        var size = GetSize(named, inlineArray);
        if (size <= MaxByValueSize || !IsFlaggedOwner(parameter, context))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            parameter.GetLocation(),
            parameter.Identifier.ValueText,
            named.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            size));
    }

    private static INamedTypeSymbol? GetMeasurableStruct(ITypeSymbol type)
    {
        var candidate = TypeSymbols.UnwrapNullable(type);

        return candidate is INamedTypeSymbol named
            && named.IsValueType
            && named.TypeKind is not TypeKind.Enum
            && !named.IsRefLikeType
            && !named.IsTupleType
            && named.DeclaringSyntaxReferences.Length is not 0
            && !IsExcludedNamespace(named)
            && !IsExplicitLayout(named)
            ? named
            : null;
    }

    private static bool HasByReferenceModifier(ParameterSyntax parameter)
    {
        foreach (var modifier in parameter.Modifiers)
        {
            if (modifier.IsKind(SyntaxKind.InKeyword)
                || modifier.IsKind(SyntaxKind.RefKeyword)
                || modifier.IsKind(SyntaxKind.OutKeyword)
                || modifier.IsKind(SyntaxKind.ParamsKeyword)
                || modifier.IsKind(SyntaxKind.ThisKeyword))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsFlaggedOwner(ParameterSyntax parameter, SyntaxNodeAnalysisContext context)
    {
        if (parameter.Parent?.Parent is not BaseMethodDeclarationSyntax owner)
        {
            return false;
        }

        if (owner is not MethodDeclarationSyntax method)
        {
            return true;
        }

        if (method.Parent is InterfaceDeclarationSyntax
            || method.Modifiers.Any(SyntaxKind.OverrideKeyword)
            || method.ExplicitInterfaceSpecifier is not null)
        {
            return false;
        }

        var symbol = context.SemanticModel.GetDeclaredSymbol(method, context.CancellationToken);
        return symbol is not IMethodSymbol methodSymbol || !InterfaceMembers.ImplementsMember(methodSymbol);
    }

    private static bool IsExcludedNamespace(INamedTypeSymbol type)
    {
        var ns = type.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        return ns.StartsWith("System.Runtime.Intrinsics", StringComparison.Ordinal)
               || ns.StartsWith("System.Numerics", StringComparison.Ordinal);
    }

    private static bool IsExplicitLayout(INamedTypeSymbol type)
    {
        foreach (var attribute in type.GetAttributes())
        {
            if (attribute.AttributeClass is { } attributeClass
                && string.Equals(attributeClass.Name, "StructLayoutAttribute", StringComparison.Ordinal)
                && string.Equals(
                    attributeClass.ContainingNamespace?.ToDisplayString(),
                    "System.Runtime.InteropServices",
                    StringComparison.Ordinal)
                && attribute.ConstructorArguments.Length is 1
                && attribute.ConstructorArguments[0].Value is int layoutKind
                && layoutKind == (int)LayoutKind.Explicit)
            {
                return true;
            }
        }

        return false;
    }

    private static int GetSize(ITypeSymbol type, INamedTypeSymbol? inlineArray)
    {
        return GetSize(type, inlineArray, new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default), 0);
    }

    private static int GetSize(
        ITypeSymbol type,
        INamedTypeSymbol? inlineArray,
        HashSet<INamedTypeSymbol> visited,
        int depth)
    {
        if (depth > MaxDepth)
        {
            return MaxByValueSize + 1;
        }

        if (GetShallowSize(type, inlineArray, visited, depth) is { } shallowSize)
        {
            return shallowSize;
        }

        if (type is not INamedTypeSymbol named || named.IsReferenceType || !visited.Add(named))
        {
            return PointerSize;
        }

        try
        {
            return TryGetInlineArraySize(named, inlineArray, visited, depth, out var inlineArraySize)
                ? inlineArraySize
                : GetFieldSize(named, inlineArray, visited, depth);
        }
        finally
        {
            _ = visited.Remove(named);
        }
    }

    // Returns the size for a pointer, a nullable, a primitive, or an enum. Returns null when the
    // type is a struct or class that the caller must measure field by field.
    private static int? GetShallowSize(
        ITypeSymbol type,
        INamedTypeSymbol? inlineArray,
        HashSet<INamedTypeSymbol> visited,
        int depth)
    {
        if (type is IArrayTypeSymbol or IPointerTypeSymbol or IFunctionPointerTypeSymbol)
        {
            return PointerSize;
        }

        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
            && nullable.TypeArguments.Length is 1)
        {
            return GetSize(nullable.TypeArguments[0], inlineArray, visited, depth + 1) + NullableTagSize;
        }

        var primitiveSize = GetPrimitiveSize(type.SpecialType);
        if (primitiveSize > 0)
        {
            return primitiveSize;
        }

        return type.TypeKind is TypeKind.Enum
               && type is INamedTypeSymbol { EnumUnderlyingType: { } underlying }
            ? GetSize(underlying, inlineArray, visited, depth + 1)
            : null;
    }

    private static int GetPrimitiveSize(SpecialType specialType)
    {
        if (specialType is SpecialType.System_Boolean or SpecialType.System_Byte or SpecialType.System_SByte)
        {
            return ByteSize;
        }

        if (specialType is SpecialType.System_Char or SpecialType.System_Int16 or SpecialType.System_UInt16)
        {
            return ShortSize;
        }

        if (specialType is SpecialType.System_Int32 or SpecialType.System_UInt32 or SpecialType.System_Single)
        {
            return IntSize;
        }

        return GetWideSize(specialType);
    }

    private static int GetWideSize(SpecialType specialType)
    {
        if (specialType is SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Double)
        {
            return LongSize;
        }

        if (specialType is SpecialType.System_Decimal)
        {
            return DecimalSize;
        }

        return specialType is SpecialType.System_IntPtr
            or SpecialType.System_UIntPtr
            or SpecialType.System_String
            or SpecialType.System_Object
            ? PointerSize
            : 0;
    }

    private static int GetFieldSize(
        INamedTypeSymbol type,
        INamedTypeSymbol? inlineArray,
        HashSet<INamedTypeSymbol> visited,
        int depth)
    {
        var total = 0;
        foreach (var member in type.GetMembers())
        {
            if (member is not IFieldSymbol { IsStatic: false } field)
            {
                continue;
            }

            var fieldSize = field.FixedSize > 1 && field.Type is IPointerTypeSymbol pointer
                ? GetSize(pointer.PointedAtType, inlineArray, visited, depth + 1) * field.FixedSize
                : GetSize(field.Type, inlineArray, visited, depth + 1);

            total += fieldSize;
            if (total > MaxByValueSize)
            {
                return total;
            }
        }

        return total;
    }

    private static bool TryGetInlineArraySize(
        INamedTypeSymbol type,
        INamedTypeSymbol? inlineArray,
        HashSet<INamedTypeSymbol> visited,
        int depth,
        out int size)
    {
        size = 0;
        if (inlineArray is null)
        {
            return false;
        }

        foreach (var attribute in type.GetAttributes())
        {
            if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, inlineArray)
                || attribute.ConstructorArguments.Length is not 1
                || attribute.ConstructorArguments[0].Value is not int count
                || count <= 0)
            {
                continue;
            }

            var element = type.GetMembers().OfType<IFieldSymbol>().FirstOrDefault(static field => !field.IsStatic);
            if (element is null)
            {
                return false;
            }

            size = GetSize(element.Type, inlineArray, visited, depth + 1) * count;
            return true;
        }

        return false;
    }
}

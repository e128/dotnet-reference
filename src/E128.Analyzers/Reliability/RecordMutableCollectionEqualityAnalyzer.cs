using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128177: Reports a record member whose type is a mutable collection
///     (<c lang="csharp">List&lt;T&gt;</c>, <c lang="csharp">Dictionary&lt;K,V&gt;</c>,
///     <c lang="csharp">HashSet&lt;T&gt;</c>, an array, <c lang="csharp">ICollection&lt;T&gt;</c>,
///     or <c lang="csharp">IList&lt;T&gt;</c>). The compiler-generated equality members compare the
///     member by reference, so two records with equal contents compare unequal. A mutation after
///     construction also breaks hash-based lookup.
/// </summary>
/// <remarks>
///     A member of an immutable collection type stays silent. The rule reports a record class and a
///     record struct. It reads the positional parameters and the declared properties and fields, and
///     it skips a static member and a computed property because neither one joins equality.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RecordMutableCollectionEqualityAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128177";

    private static readonly ImmutableArray<string> MutableCollectionMetadataNames =
    [
        "System.Collections.Generic.List`1",
        "System.Collections.Generic.Dictionary`2",
        "System.Collections.Generic.HashSet`1",
        "System.Collections.Generic.ICollection`1",
        "System.Collections.Generic.IList`1"
    ];

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Record equality over a mutable collection",
        "Record member '{0}' has mutable collection type '{1}', so the generated equality compares it by reference",
        "E128-Reliability",
        DiagnosticSeverity.Warning,
        true,
        "The compiler-generated Equals and GetHashCode for a record compare a mutable collection " +
        "member by reference. Two records with equal contents compare unequal, and a mutation " +
        "after construction breaks hash-based lookup. Use an immutable collection type, or " +
        "declare the equality members yourself.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var mutableTypes = FindMutableTypes(start.Compilation);

            if (mutableTypes.IsEmpty)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(
                ctx => AnalyzeRecord(ctx, mutableTypes),
                SyntaxKind.RecordDeclaration);
            start.RegisterSyntaxNodeAction(
                ctx => AnalyzeRecord(ctx, mutableTypes),
                SyntaxKind.RecordStructDeclaration);
        });
    }

    private static ImmutableArray<INamedTypeSymbol> FindMutableTypes(Compilation compilation)
    {
        var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();

        foreach (var metadataName in MutableCollectionMetadataNames)
        {
            builder.AddRange(MetadataTypes.Find(compilation, metadataName));
        }

        return builder.ToImmutable();
    }

    private static void AnalyzeRecord(
        SyntaxNodeAnalysisContext context,
        ImmutableArray<INamedTypeSymbol> mutableTypes)
    {
        var record = (RecordDeclarationSyntax)context.Node;

        // A record that declares GetHashCode owns its equality, which is the fix this rule asks for.
        if (DeclaresGetHashCode(record))
        {
            return;
        }

        AnalyzePositionalParameters(context, record, mutableTypes);

        foreach (var member in record.Members)
        {
            if (member is PropertyDeclarationSyntax property)
            {
                AnalyzeProperty(context, property, mutableTypes);
            }
            else if (member is FieldDeclarationSyntax field)
            {
                AnalyzeField(context, field, mutableTypes);
            }
        }
    }

    private static bool DeclaresGetHashCode(RecordDeclarationSyntax record)
    {
        foreach (var member in record.Members)
        {
            if (member is MethodDeclarationSyntax { ParameterList.Parameters.Count: 0 } method
                && string.Equals(method.Identifier.ValueText, "GetHashCode", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static void AnalyzePositionalParameters(
        SyntaxNodeAnalysisContext context,
        RecordDeclarationSyntax record,
        ImmutableArray<INamedTypeSymbol> mutableTypes)
    {
        if (record.ParameterList is not { } parameterList)
        {
            return;
        }

        foreach (var parameter in parameterList.Parameters)
        {
            if (context.SemanticModel.GetDeclaredSymbol(parameter, context.CancellationToken)
                is not IParameterSymbol parameterSymbol)
            {
                continue;
            }

            if (IsMutableCollection(parameterSymbol.Type, mutableTypes))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Rule,
                    parameter.GetLocation(),
                    parameterSymbol.Name,
                    parameterSymbol.Type.ToDisplayString()));
            }
        }
    }

    private static void AnalyzeProperty(
        SyntaxNodeAnalysisContext context,
        PropertyDeclarationSyntax property,
        ImmutableArray<INamedTypeSymbol> mutableTypes)
    {
        if (!IsAutoProperty(property))
        {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(property, context.CancellationToken)
            is not IPropertySymbol propertySymbol)
        {
            return;
        }

        if (IsMutableCollection(propertySymbol.Type, mutableTypes))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Rule,
                property.GetLocation(),
                propertySymbol.Name,
                propertySymbol.Type.ToDisplayString()));
        }
    }

    private static void AnalyzeField(
        SyntaxNodeAnalysisContext context,
        FieldDeclarationSyntax field,
        ImmutableArray<INamedTypeSymbol> mutableTypes)
    {
        if (field.Modifiers.Any(SyntaxKind.StaticKeyword))
        {
            return;
        }

        foreach (var variable in field.Declaration.Variables)
        {
            if (context.SemanticModel.GetDeclaredSymbol(variable, context.CancellationToken)
                    is not IFieldSymbol fieldSymbol
                || fieldSymbol.IsConst)
            {
                continue;
            }

            if (IsMutableCollection(fieldSymbol.Type, mutableTypes))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Rule,
                    variable.GetLocation(),
                    fieldSymbol.Name,
                    fieldSymbol.Type.ToDisplayString()));
            }
        }
    }

    private static bool IsMutableCollection(
        ITypeSymbol type,
        ImmutableArray<INamedTypeSymbol> mutableTypes)
    {
        return type is IArrayTypeSymbol
            || (type is INamedTypeSymbol named
                && MetadataTypes.Contains(mutableTypes, named.OriginalDefinition));
    }

    private static bool IsAutoProperty(PropertyDeclarationSyntax property)
    {
        // A static property never joins equality. An abstract property has no backing field, so it
        // never joins equality either.
        if (property.Modifiers.Any(SyntaxKind.StaticKeyword)
            || property.Modifiers.Any(SyntaxKind.AbstractKeyword))
        {
            return false;
        }

        // A property with a body is computed. It has no backing field, so equality ignores it.
        if (property.AccessorList is not { } accessorList)
        {
            return false;
        }

        var accessors = accessorList.Accessors;
        if (accessors.Count == 0)
        {
            return false;
        }

        foreach (var accessor in accessors)
        {
            if (accessor.Body is not null || accessor.ExpressionBody is not null)
            {
                return false;
            }
        }

        return true;
    }
}

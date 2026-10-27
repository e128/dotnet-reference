using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DllImportToLibraryImportAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128117";

    private static readonly HashSet<SpecialType> BlittableSpecialTypes =
    [
        SpecialType.System_SByte,
        SpecialType.System_Byte,
        SpecialType.System_Int16,
        SpecialType.System_UInt16,
        SpecialType.System_Int32,
        SpecialType.System_UInt32,
        SpecialType.System_Single,
        SpecialType.System_Int64,
        SpecialType.System_UInt64,
        SpecialType.System_Double,
        SpecialType.System_IntPtr,
        SpecialType.System_UIntPtr,
    ];

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Use LibraryImport instead of DllImport",
        "LibraryImport generates the marshalling code at compile time. DllImport resolves it at run time.",
        "Performance",
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
        context.RegisterCompilationStartAction(start =>
        {
            // Without LibraryImportAttribute (before .NET 7) the rule has no remedy.
            if (MetadataTypes.Find(start.Compilation, "System.Runtime.InteropServices.LibraryImportAttribute").IsEmpty)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(AnalyzeAttribute, SyntaxKind.Attribute);
        });
    }

    private static void AnalyzeAttribute(SyntaxNodeAnalysisContext context)
    {
        var attribute = (AttributeSyntax)context.Node;

        if (!string.Equals(GetAttributeName(attribute), "DllImport", StringComparison.Ordinal)
            || attribute.Parent?.Parent is not MethodDeclarationSyntax)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(attribute, context.CancellationToken).Symbol
            is not IMethodSymbol { ContainingType.Name: "DllImportAttribute" })
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, attribute.GetLocation()));
    }

    internal static string? GetAttributeName(AttributeSyntax attribute)
    {
        return attribute.Name switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            QualifiedNameSyntax { Right: IdentifierNameSyntax right } => right.Identifier.ValueText,
            _ => null,
        };
    }

    internal static bool IsBlittableSignature(IMethodSymbol method)
    {
        foreach (var parameter in method.Parameters)
        {
            if (!IsBlittable(parameter.Type))
            {
                return false;
            }
        }

        return method.ReturnsVoid || IsBlittable(method.ReturnType);
    }

    private static bool IsBlittable(ITypeSymbol type)
    {
        return type.TypeKind is TypeKind.Pointer or TypeKind.Enum
            || BlittableSpecialTypes.Contains(type.SpecialType);
    }
}

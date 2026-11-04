using System;
using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Design;

/// <summary>
///     E128174: Flags a constructor, or a primary constructor, that takes more reference-type dependencies than the
///     <c lang="csharp">e128_max_ctor_dependencies</c> ceiling. A long dependency list marks a type with too many jobs.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ConstructorDependencyCeilingAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128174";

    internal const string OptionKey = "e128_max_ctor_dependencies";

    private const int DefaultCeiling = 7;

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Reduce constructor dependencies",
        "'{0}' takes {1} dependencies, over the ceiling of {2}: split the type by responsibility",
        "E128-Design",
        DiagnosticSeverity.Warning,
        true,
        "A constructor with many class or interface parameters marks a type that does several jobs. Split it, or group related dependencies behind one service. Set e128_max_ctor_dependencies to change the ceiling.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeConstructor, SyntaxKind.ConstructorDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzePrimaryConstructor, SyntaxKind.ClassDeclaration, SyntaxKind.StructDeclaration);
    }

    private static void AnalyzeConstructor(SyntaxNodeAnalysisContext context)
    {
        var constructor = (ConstructorDeclarationSyntax)context.Node;
        Check(context, constructor.ParameterList, constructor.Identifier);
    }

    private static void AnalyzePrimaryConstructor(SyntaxNodeAnalysisContext context)
    {
        var type = (TypeDeclarationSyntax)context.Node;

        if (type.ParameterList is { } parameters)
        {
            Check(context, parameters, type.Identifier);
        }
    }

    private static void Check(SyntaxNodeAnalysisContext context, ParameterListSyntax parameters, SyntaxToken identifier)
    {
        var ceiling = ReadCeiling(context);
        var count = 0;

        foreach (var parameter in parameters.Parameters)
        {
            if (context.SemanticModel.GetDeclaredSymbol(parameter, context.CancellationToken)?.Type is { } type
                && IsDependency(type))
            {
                count++;
            }
        }

        if (count > ceiling)
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, identifier.GetLocation(), identifier.ValueText, count, ceiling));
        }
    }

    private static bool IsDependency(ITypeSymbol type)
    {
        return type.SpecialType != SpecialType.System_String
               && type.TypeKind is TypeKind.Class or TypeKind.Interface;
    }

    private static int ReadCeiling(SyntaxNodeAnalysisContext context)
    {
        var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree);

        return options.TryGetValue(OptionKey, out var text)
               && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
               && value > 0
            ? value
            : DefaultCeiling;
    }
}

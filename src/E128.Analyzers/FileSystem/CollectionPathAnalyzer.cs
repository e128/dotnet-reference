using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.FileSystem;

/// <summary>
///     E128053: Flags parameters typed as a collection of strings (e.g., <c>IReadOnlyList&lt;string&gt;</c>)
///     whose name suggests file system paths. Use <c>FileInfo</c> or <c>DirectoryInfo</c> as the
///     element type instead of <see langword="string" />.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CollectionPathAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128053";
    internal const string SuggestedTypeKey = "SuggestedType";

    internal static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Use collection of FileInfo or DirectoryInfo instead of collection of string for file system paths",
        "Parameter '{0}' appears to represent a collection of {1}. Consider using '{2}' instead of '{3}'.",
        "Design",
        DiagnosticSeverity.Warning,
        true);

    // Generic collection type names that, when parameterized with string, suggest FileInfo/DirectoryInfo.
    private static readonly ImmutableArray<string> CollectionTypeNames =
    [
        "IReadOnlyList", "IList", "List",
        "IEnumerable", "ICollection", "IReadOnlyCollection",
        "ImmutableArray"
    ];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeNode,
            SyntaxKind.MethodDeclaration,
            SyntaxKind.ConstructorDeclaration,
            SyntaxKind.RecordDeclaration);
    }

    private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        var paramList = context.Node switch
        {
            MethodDeclarationSyntax method => method.ParameterList,
            ConstructorDeclarationSyntax ctor => ctor.ParameterList,
            RecordDeclarationSyntax record => record.ParameterList,
            _ => null
        };

        if (paramList is null)
        {
            return;
        }

        foreach (var param in paramList.Parameters)
        {
            AnalyzeParameter(context, param);
        }
    }

    private static void AnalyzeParameter(SyntaxNodeAnalysisContext context, ParameterSyntax param)
    {
        if (!TryGetStringCollectionType(param.Type, out var genericName))
        {
            return;
        }

        var paramName = param.Identifier.ValueText;
        if (!PathNamePatterns.IsPathName(paramName))
        {
            return;
        }

        var (description, suggestedType) = GetSuggestion(paramName);
        var originalType = genericName.Identifier.ValueText + "<string>";
        var suggestedCollectionType = genericName.Identifier.ValueText + "<" + suggestedType + ">";

        var properties = ImmutableDictionary.CreateRange(
            [
                new KeyValuePair<string, string?>(SuggestedTypeKey, suggestedType)
            ]);

        context.ReportDiagnostic(Diagnostic.Create(Rule,
            param.Identifier.GetLocation(),
            properties,
            paramName, description, suggestedCollectionType, originalType));
    }

    private static bool TryGetStringCollectionType(
        TypeSyntax? type,
        [NotNullWhen(true)] out GenericNameSyntax? genericName)
    {
        genericName = null;

        // Handle both unqualified (IReadOnlyList<string>) and qualified (System.Collections.Generic.List<string>)
        var candidate = type switch
        {
            GenericNameSyntax direct => direct,
            QualifiedNameSyntax qualified when qualified.Right is GenericNameSyntax nested => nested,
            _ => null
        };

        if (candidate is null)
        {
            return false;
        }

        if (!IsKnownCollectionType(candidate.Identifier.ValueText))
        {
            return false;
        }

        var typeArgs = candidate.TypeArgumentList.Arguments;
        if (typeArgs.Count != 1)
        {
            return false;
        }

        if (typeArgs[0] is not PredefinedTypeSyntax { Keyword.ValueText: "string" })
        {
            return false;
        }

        genericName = candidate;
        return true;
    }

    private static bool IsKnownCollectionType(string typeName)
    {
        foreach (var known in CollectionTypeNames)
        {
            if (string.Equals(typeName, known, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static (string Description, string SuggestedType) GetSuggestion(string paramName)
    {
        if (paramName.IndexOf("dir", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return ("directory paths", "DirectoryInfo");
        }

        return ("file paths", "FileInfo");
    }
}

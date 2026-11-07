using System;
using System.Collections.Immutable;
using System.Linq;
using E128.Analyzers.Testing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Security;

/// <summary>
///     E128164: Flags an absolute <c lang="csharp">http</c> or <c lang="csharp">https</c> URL written as a string
///     literal in production code. A URL belongs in a strongly-typed options class bound to configuration.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HardcodedUrlAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128164";

    // Namespace and schema identifiers are names, not endpoints. They stay silent.
    private static readonly string[] AllowedHostSuffixes =
    [
        "w3.org",
        "schemas.microsoft.com",
        "schemas.openxmlformats.org",
        "schemas.xmlsoap.org",
        "json-schema.org",
        "example.com",
        "example.org",
        "example.net"
    ];

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Avoid hardcoded URL",
        "Hardcoded URL '{0}': expose it through an options class bound to configuration",
        "E128-Security",
        DiagnosticSeverity.Warning,
        true,
        "An absolute http or https URL in source ties the build to one environment. Expose the value through a strongly-typed options class bound to configuration.");

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
            if (XunitTestHelper.IsTestCompilation(start.Compilation))
            {
                return;
            }

            start.RegisterSyntaxNodeAction(AnalyzeLiteral, SyntaxKind.StringLiteralExpression);
            start.RegisterSyntaxNodeAction(AnalyzeInterpolatedText, SyntaxKind.InterpolatedStringText);
        });
    }

    private static void AnalyzeLiteral(SyntaxNodeAnalysisContext context)
    {
        var literal = (LiteralExpressionSyntax)context.Node;
        Report(context, literal.Token.ValueText);
    }

    private static void AnalyzeInterpolatedText(SyntaxNodeAnalysisContext context)
    {
        var text = (InterpolatedStringTextSyntax)context.Node;
        Report(context, text.TextToken.ValueText);
    }

    private static void Report(SyntaxNodeAnalysisContext context, string value)
    {
        if (!IsHardcodedUrl(value) || AttributeArguments.IsInside(context.Node) || IsExemptDeclaration(context.Node))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, context.Node.GetLocation(), value));
    }

    // A property initializer on an options class, and a const or static readonly field, each declare
    // one fixed value in one place. The rule asks for exactly that shape, so neither is the endpoint
    // per environment it exists to catch. A local const stays reported: it hides a value in a code path.
    private static bool IsExemptDeclaration(SyntaxNode node)
    {
        var declaration = node.Ancestors().FirstOrDefault(static ancestor =>
            ancestor is PropertyDeclarationSyntax
                or VariableDeclaratorSyntax
                or MethodDeclarationSyntax
                or LocalFunctionStatementSyntax
                or AccessorDeclarationSyntax);

        return declaration switch
        {
            PropertyDeclarationSyntax { Initializer: not null } property => IsOptionsClass(property),
            VariableDeclaratorSyntax declarator => IsConstantField(declarator),
            _ => false
        };
    }

    private static bool IsOptionsClass(PropertyDeclarationSyntax property)
    {
        return property.Parent is TypeDeclarationSyntax type
               && type.Identifier.ValueText.EndsWith("Options", StringComparison.Ordinal);
    }

    private static bool IsConstantField(VariableDeclaratorSyntax declarator)
    {
        return declarator.Parent?.Parent is FieldDeclarationSyntax field
               && (field.Modifiers.Any(SyntaxKind.ConstKeyword)
                   || (field.Modifiers.Any(SyntaxKind.StaticKeyword)
                       && field.Modifiers.Any(SyntaxKind.ReadOnlyKeyword)));
    }

    private static bool IsHardcodedUrl(string value)
    {
        if (!value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Host.Length == 0)
        {
            return false;
        }

        var host = uri.Host;
        foreach (var suffix in AllowedHostSuffixes)
        {
            // Match the host itself or a subdomain. A bare suffix match would allow evilexample.com.
            if (string.Equals(host, suffix, StringComparison.OrdinalIgnoreCase)
                || host.EndsWith("." + suffix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}

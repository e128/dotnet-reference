using System;
using System.Collections.Immutable;
using E128.Analyzers.Testing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Security;

/// <summary>
///     E128165: Flags a credential written as a string literal in production code. The rule reads two shapes:
///     a literal bound to a credential name such as <c lang="csharp">password</c> or
///     <c lang="csharp">apiKey</c>, and a connection string that carries a password.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HardcodedSecretAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128165";

    private const int MinimumSecretLength = 8;

    private static readonly string[] CredentialWords =
    [
        "password",
        "passwd",
        "pwd",
        "secret",
        "apikey",
        "accesskey",
        "privatekey",
        "connectionstring"
    ];

    // A name that ends in one of these words names a lookup key, a header, or a label. The value is
    // not the credential.
    private static readonly string[] NonCredentialSuffixes =
    [
        "name",
        "header",
        "claim",
        "type",
        "prefix",
        "scheme",
        "path",
        "label",
        "format",
        "field",
        "param",
        "parameter",
        "setting",
        "settingkey",
        "configkey",
        "configurationkey"
    ];

    private static readonly string[] ServerKeys = ["server=", "data source=", "host="];

    private static readonly string[] PasswordKeys = ["password=", "pwd="];

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Avoid hardcoded secret",
        "Hardcoded credential in '{0}': expose it through an options class bound to configuration",
        "E128-Security",
        DiagnosticSeverity.Warning,
        true,
        "A password, key, or connection string in source ships with every copy of the code. Expose the value through a strongly-typed options class bound to configuration.");

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
        });
    }

    private static void AnalyzeLiteral(SyntaxNodeAnalysisContext context)
    {
        var literal = (LiteralExpressionSyntax)context.Node;
        var value = literal.Token.ValueText;

        if (HasConnectionStringPassword(value))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, literal.GetLocation(), "connection string"));
            return;
        }

        if (value.Length < MinimumSecretLength || ContainsWhitespace(value))
        {
            return;
        }

        var name = TargetName(literal);

        if (name is not null && IsCredentialName(name))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, literal.GetLocation(), name));
        }
    }

    private static string? TargetName(LiteralExpressionSyntax literal)
    {
        return literal.Parent switch
        {
            EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator } => declarator.Identifier.ValueText,
            EqualsValueClauseSyntax { Parent: PropertyDeclarationSyntax property } => property.Identifier.ValueText,
            ArrowExpressionClauseSyntax { Parent: PropertyDeclarationSyntax property } => property.Identifier.ValueText,
            AssignmentExpressionSyntax assignment when assignment.Right == literal => SimpleName(assignment.Left),
            ArgumentSyntax { NameColon: { } nameColon } => nameColon.Name.Identifier.ValueText,
            _ => null
        };
    }

    private static string? SimpleName(ExpressionSyntax expression)
    {
        return expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
            _ => null
        };
    }

    private static bool IsCredentialName(string name)
    {
        var normalized = name.Replace("_", string.Empty);

        foreach (var suffix in NonCredentialSuffixes)
        {
            if (normalized.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        foreach (var word in CredentialWords)
        {
            if (normalized.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasConnectionStringPassword(string value)
    {
        return ContainsAny(value, ServerKeys) && HasPasswordValue(value);
    }

    private static bool HasPasswordValue(string text)
    {
        foreach (var key in PasswordKeys)
        {
            var index = text.IndexOf(key, StringComparison.OrdinalIgnoreCase);

            if (index < 0)
            {
                continue;
            }

            var valueStart = index + key.Length;

            // A format placeholder or an empty value is not a hardcoded password.
            if (valueStart < text.Length && text[valueStart] != ';' && text[valueStart] != '{')
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsAny(string text, string[] keys)
    {
        foreach (var key in keys)
        {
            if (text.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsWhitespace(string value)
    {
        foreach (var character in value)
        {
            if (char.IsWhiteSpace(character))
            {
                return true;
            }
        }

        return false;
    }
}

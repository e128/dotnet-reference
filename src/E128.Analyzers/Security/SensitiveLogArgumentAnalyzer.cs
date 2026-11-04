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
///     E128166: Flags an <c lang="csharp">ILogger</c> call that passes a value whose name holds a credential or
///     personal-data word, such as <c lang="csharp">password</c> or <c lang="csharp">token</c>. A log line outlives the
///     request and reaches every reader of the log store.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SensitiveLogArgumentAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128166";

    private static readonly string[] SensitiveWords =
    [
        "password",
        "passwd",
        "pwd",
        "secret",
        "apikey",
        "accesskey",
        "privatekey",
        "connectionstring",
        "token",
        "ssn",
        "creditcard",
        "cardnumber"
    ];

    // A name with one of these endings, or the word "cancellation", holds metadata or a framework
    // type, not the sensitive value.
    private static readonly string[] SafeSuffixes =
    [
        "name",
        "type",
        "count",
        "length",
        "expiry",
        "header",
        "id",
        "hint"
    ];

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Avoid logging a sensitive value",
        "Logger call passes '{0}', a credential or personal-data value: log an identifier or a redacted form instead",
        "E128-Security",
        DiagnosticSeverity.Warning,
        true,
        "A log entry outlives the request and reaches every reader of the log store. Log an identifier or a redacted form of a credential or personal-data value.");

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

            start.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        });
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !IsLoggerMethod(method))
        {
            return;
        }

        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            foreach (var expression in ValueExpressions(argument.Expression))
            {
                var name = LeafName(expression);

                if (name is not null && IsSensitiveName(name))
                {
                    context.ReportDiagnostic(Diagnostic.Create(Rule, argument.GetLocation(), name));
                    break;
                }
            }
        }
    }

    private static bool IsLoggerMethod(IMethodSymbol method)
    {
        var typeName = method.ContainingType.ToDisplayString();

        return string.Equals(typeName, "Microsoft.Extensions.Logging.LoggerExtensions", StringComparison.Ordinal)
               || string.Equals(typeName, "Microsoft.Extensions.Logging.ILogger", StringComparison.Ordinal);
    }

    // An interpolated argument hides its values in holes. Read each hole as its own value.
    private static ExpressionSyntax[] ValueExpressions(ExpressionSyntax expression)
    {
        return expression is InterpolatedStringExpressionSyntax interpolated
            ? [.. interpolated.Contents.OfType<InterpolationSyntax>().Select(hole => hole.Expression)]
            : [expression];
    }

    private static string? LeafName(ExpressionSyntax expression)
    {
        return expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
            _ => null
        };
    }

    private static bool IsSensitiveName(string name)
    {
        var normalized = name.Replace("_", string.Empty);

        if (normalized.IndexOf("cancellation", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return false;
        }

        foreach (var suffix in SafeSuffixes)
        {
            if (normalized.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        foreach (var word in SensitiveWords)
        {
            if (normalized.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }
}

using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Design;

/// <summary>
///     Code fix for E128060: appends <c lang="csharp">.AsReadOnly()</c> to the return expression
///     so the caller receives a <c lang="csharp">ReadOnlyDictionary&lt;K,V&gt;</c> wrapper rather than
///     the raw mutable dictionary.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DictionaryAsReadOnlyCodeFixProvider))]
[Shared]
public sealed class DictionaryAsReadOnlyCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [DictionaryAsReadOnlyAnalyzer.DiagnosticId];

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        RegisterNodeFix<ExpressionSyntax>(
            context,
            root,
            "Return .AsReadOnly()",
            nameof(DictionaryAsReadOnlyCodeFixProvider),
            (expression, ct) => ReadOnlyCollectionFix.AppendAsReadOnlyAsync(context.Document, expression, ct));

        return Task.CompletedTask;
    }
}

using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Design;

/// <summary>
///     Code fix for E128058: appends <c lang="csharp">.AsReadOnly()</c> to the return expression
///     so the caller receives a <c lang="csharp">ReadOnlyCollection&lt;T&gt;</c> wrapper rather than
///     the raw mutable list.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ListAsReadOnlyCodeFixProvider))]
[Shared]
public sealed class ListAsReadOnlyCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [ListAsReadOnlyAnalyzer.DiagnosticId];

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        RegisterNodeFix<ExpressionSyntax>(
            context,
            root,
            "Return .AsReadOnly()",
            nameof(ListAsReadOnlyCodeFixProvider),
            (expression, ct) => ReadOnlyCollectionFix.AppendAsReadOnlyAsync(context.Document, expression, ct));

        return Task.CompletedTask;
    }
}

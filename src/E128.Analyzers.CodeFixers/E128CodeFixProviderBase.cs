using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;

namespace E128.Analyzers;

/// <summary>
///     Shared base for this repository's code fix providers. One place owns the
///     batch fix-all provider and the syntax root fetch that most fixes start with.
///     A provider that does not start from the syntax root overrides
///     <see cref="RegisterCodeFixesAsync"/> directly instead of the hook.
/// </summary>
public abstract class E128CodeFixProviderBase : CodeFixProvider
{
    /// <summary>
    ///     The batch fix-all provider. A fixer whose edits cannot be fix-all'd,
    ///     for example a sequential rename, overrides this to return null.
    /// </summary>
    public override FixAllProvider? GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        await RegisterFixesAsync(context, root).ConfigureAwait(false);
    }

    /// <summary>
    ///     The provider-specific work, called only when the syntax root resolved. A
    ///     provider that registers from the diagnostic alone overrides
    ///     <see cref="RegisterCodeFixesAsync"/> directly and never reaches this hook.
    /// </summary>
    protected virtual Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        return Task.CompletedTask;
    }
}

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeFixes;

namespace E128.Analyzers;

/// <summary>
///     A code fix provider that intentionally registers no code action. Some diagnostics are too
///     structural to fix mechanically. The provider still ships so the IDE lists the fixer for the
///     diagnostic ID and the "Suppress or Configure" entry appears, while the developer applies the
///     fix by hand. The fix-all provider is null because there is nothing to fix.
/// </summary>
public abstract class E128NoOpCodeFixProvider : E128CodeFixProviderBase
{
    /// <inheritdoc />
    public override FixAllProvider? GetFixAllProvider()
    {
        return null;
    }

    /// <inheritdoc />
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        return Task.CompletedTask;
    }
}

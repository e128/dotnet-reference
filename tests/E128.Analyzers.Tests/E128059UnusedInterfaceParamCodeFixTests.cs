using System;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.CodeFixes;
using Xunit;

namespace E128.Analyzers.Tests;

/// <summary>
///     Registration test for the UnusedInterfaceParam code fix provider.
///     Rename-based code fix tests require workspace support not available in the unit test harness.
/// </summary>
public sealed class E128059UnusedInterfaceParamCodeFixTests
{
    [Fact]
    [Trait("Category", "CI")]
    public void UnusedInterfaceParam_CodeFixProvider_IsRegistered()
    {
        var provider = new UnusedInterfaceParamCodeFixProvider();
        Assert.Contains("E128059", provider.FixableDiagnosticIds, StringComparer.Ordinal);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void UnusedInterfaceParam_FixAll_DoesNotUseBatchFixer()
    {
        // A batch merge applies two renames from the same snapshot and loses one of them.
        var fixAll = new UnusedInterfaceParamCodeFixProvider().GetFixAllProvider();

        Assert.NotNull(fixAll);
        Assert.NotSame(WellKnownFixAllProviders.BatchFixer, fixAll);
    }
}

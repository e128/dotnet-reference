using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

/// <summary>
///     Registration test for the FileInfoToctou code fix provider.
///     Full code fix output tests are deferred — see analyzer tests for diagnostic coverage.
/// </summary>
public sealed class E128056FileInfoToctouCodeFixTests
{
    [Fact]
    [Trait("Category", "CI")]
    public void FileInfoToctou_CodeFixProvider_IsRegistered()
    {
        var provider = new FileInfoToctouCodeFixProvider();
        Assert.Contains("E128056", provider.FixableDiagnosticIds);
    }
}

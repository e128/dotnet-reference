using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class AsyncDbIoAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        return VerifyAsync(TestStubs.AsyncDbIoFakeConnection, code, expected);
    }

    private static Task VerifyAsync(string prefix, string code, params DiagnosticResult[] expected)
    {
        return AnalyzerTestRunner.VerifyAsync<AsyncDbIoAnalyzer>(
            prefix + "\n" + code,
            expected,
            new AnalyzerTestOptions { MarkupOptions = MarkupOptions.UseFirstDescriptor });
    }

    /// <summary>
    ///     AC-1: E128093 fires on a sync DbConnection call inside a fully synchronous method, resolving the async sibling
    ///     declared on the DbConnection base type.
    /// </summary>
    [Fact]
    [Trait("Category", "CI")]
    public Task AsyncDbIoAnalyzer_Fires_OnSyncDbConnectionCallInSynchronousMethod()
    {
        return VerifyAsync("""
                           class C
                           {
                               void M(FakeConnection connection)
                               {
                                   {|E128093:connection.Open()|};
                               }
                           }
                           """);
    }

    /// <summary>
    ///     AC-2: E128093 does not fire when the containing method is already async.
    /// </summary>
    [Fact]
    [Trait("Category", "CI")]
    public Task AsyncDbIoAnalyzer_DoesNotFire_WhenContainingMethodAlreadyAsync()
    {
        return VerifyAsync(TestStubs.AsyncDbIoFakeConnectionWithTaskUsing, """
                                                                           class C
                                                                           {
                                                                               async Task M(FakeConnection connection)
                                                                               {
                                                                                   connection.Open();
                                                                                   await Task.CompletedTask;
                                                                               }
                                                                           }
                                                                           """);
    }

    /// <summary>
    ///     AC-3: E128093 does not fire on a DbConnection-derived member with no Async sibling.
    /// </summary>
    [Fact]
    [Trait("Category", "CI")]
    public Task AsyncDbIoAnalyzer_DoesNotFire_OnMemberWithoutAsyncSibling()
    {
        return VerifyAsync("""
                           class C
                           {
                               void M(FakeConnection connection)
                               {
                                   connection.Ping();
                               }
                           }
                           """);
    }
}

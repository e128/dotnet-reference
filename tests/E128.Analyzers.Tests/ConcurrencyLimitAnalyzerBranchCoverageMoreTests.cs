using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ConcurrencyLimitAnalyzerBranchCoverageMoreTests
{
    private static readonly AnalyzerTestOptions IgnoreCompilerErrors = new() { CompilerDiagnostics = CompilerDiagnostics.None };

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheSemaphoreCreationDoesNotBind()
    {
        return AnalyzerTestRunner.VerifyAsync<ConcurrencyLimitAnalyzer>(
            """
            using System.Threading;
            class C
            {
                void M()
                {
                    var semaphore = new SemaphoreSlim("one", 2, 3);
                }
            }
            """,
            expected: null,
            options: IgnoreCompilerErrors);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheBoundedChannelCallDoesNotBind()
    {
        return AnalyzerTestRunner.VerifyAsync<ConcurrencyLimitAnalyzer>(
            """
            using System.Threading.Channels;
            class C
            {
                void M()
                {
                    var channel = Channel.CreateBounded<int>("a", "b", "c", "d");
                }
            }
            """,
            expected: null,
            options: IgnoreCompilerErrors);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenANamedConstantIsNegated()
    {
        return AnalyzerTestRunner.VerifyAsync<ConcurrencyLimitAnalyzer>("""
            using System.Threading;
            class C
            {
                const int Limit = 3;
                SemaphoreSlim M() => {|E128040:new SemaphoreSlim(-Limit)|};
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenACharacterLiteralIsNegated()
    {
        return AnalyzerTestRunner.VerifyAsync<ConcurrencyLimitAnalyzer>("""
            using System.Threading;
            class C
            {
                SemaphoreSlim M() => {|E128040:new SemaphoreSlim(-'a')|};
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCountIsACharacterLiteral()
    {
        return AnalyzerTestRunner.VerifyAsync<ConcurrencyLimitAnalyzer>("""
            using System.Threading;
            using System.Threading.Channels;
            class C
            {
                SemaphoreSlim M() => new SemaphoreSlim('a');
                Channel<int> N() => Channel.CreateBounded<int>('a');
            }
            """);
    }
}

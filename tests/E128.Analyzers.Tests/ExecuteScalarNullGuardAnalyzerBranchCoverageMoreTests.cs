using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ExecuteScalarNullGuardAnalyzerBranchCoverageMoreTests
{
    private static Task VerifyAsync(string body, bool lenient = false)
    {
        var options = lenient ? new AnalyzerTestOptions { CompilerDiagnostics = CompilerDiagnostics.None } : null;

        return AnalyzerTestRunner.VerifyAsync<ExecuteScalarNullGuardAnalyzer>(
            $$"""
            using System;
            using System.Data.Common;
            using System.Threading.Tasks;

            class Helper
            {
                public static int ToInt32(object value) => 0;
            }

            class C
            {
                static object Fetch() => 0;

                static Task<object> FetchAsync() => Task.FromResult<object>(0);

                async Task M(DbCommand cmd, Task<object> pending)
                {
                    {{body}}
                }
            }
            """,
            options: options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenConvertIsCalledWithoutArguments()
    {
        return VerifyAsync("var x = Convert.ToInt32();", lenient: true);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheConvertCallDoesNotBind()
    {
        return VerifyAsync("var x = Convert.ToInt32(cmd.ExecuteScalar(), 1, 2, 3);", lenient: true);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAUserTypeDeclaresToInt32()
    {
        return VerifyAsync("var x = Helper.ToInt32(cmd.ExecuteScalar());");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheArgumentIsAnotherCommandMethod()
    {
        return VerifyAsync("var x = Convert.ToInt32(cmd.ExecuteNonQuery());");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheArgumentIsALocalMethodCall()
    {
        return VerifyAsync("var x = Convert.ToInt32(Fetch());");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheAwaitedCallIsAnotherCommandMethod()
    {
        return VerifyAsync("var x = Convert.ToInt32(await cmd.ExecuteNonQueryAsync());");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheAwaitedOperandIsNotAnInvocation()
    {
        return VerifyAsync("var x = Convert.ToInt32(await pending);");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheAwaitedCallIsNotAMemberAccess()
    {
        return VerifyAsync("var x = Convert.ToInt32(await FetchAsync());");
    }
}

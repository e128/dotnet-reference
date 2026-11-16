using System.Threading.Tasks;
using E128.Analyzers.Design;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ExecuteScalarNullGuardCodeFixProviderBranchCoverageTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return CodeFixTestRunner.RunAsync<ExecuteScalarNullGuardAnalyzer, ExecuteScalarNullGuardCodeFixProvider>(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_UsesTheNextFreeNameAndKeepsTheInt64Conversion()
    {
        return VerifyFixAsync(
            """
            using System;
            using System.Data.Common;

            class C
            {
                long M(DbCommand cmd, int result, int result2)
                {
                    return {|E128042:Convert.ToInt64(cmd.ExecuteScalar())|};
                }
            }
            """,
            """
            using System;
            using System.Data.Common;

            class C
            {
                long M(DbCommand cmd, int result, int result2)
                {
                    var result3 = cmd.ExecuteScalar();
                    return result3 is null ? 0 : Convert.ToInt64(result3);
                }
            }
            """);
    }


}

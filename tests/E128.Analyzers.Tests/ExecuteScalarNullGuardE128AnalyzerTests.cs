using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ExecuteScalarNullGuardE128AnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<ExecuteScalarNullGuardAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConvertToInt32_WrappingExecuteScalar_Fires()
    {
        return VerifyAsync("""
                           using System;
                           using System.Data;
                           using System.Data.Common;

                           class C
                           {
                               void M(DbCommand cmd)
                               {
                                   var count = {|E128042:Convert.ToInt32(cmd.ExecuteScalar())|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConvertToInt64_WrappingExecuteScalar_Fires()
    {
        return VerifyAsync("""
                           using System;
                           using System.Data;
                           using System.Data.Common;

                           class C
                           {
                               void M(DbCommand cmd)
                               {
                                   var count = {|E128042:Convert.ToInt64(cmd.ExecuteScalar())|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConvertToInt32_WrappingExecuteScalarAsync_Fires()
    {
        return VerifyAsync("""
                           using System;
                           using System.Data;
                           using System.Data.Common;
                           using System.Threading.Tasks;

                           class C
                           {
                               async Task M(DbCommand cmd)
                               {
                                   var count = {|E128042:Convert.ToInt32(await cmd.ExecuteScalarAsync())|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConvertToInt32_NormalValue_NoDiagnostic()
    {
        return VerifyAsync("""
                           using System;

                           class C
                           {
                               void M()
                               {
                                   var x = Convert.ToInt32("42");
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConvertToInt32_WithNullCheck_NoDiagnostic()
    {
        // The analyzer only fires when ExecuteScalar is the direct argument —
        // if the user extracts it into a local variable with a null check, no diagnostic.
        return VerifyAsync("""
                           using System;
                           using System.Data;
                           using System.Data.Common;

                           class C
                           {
                               void M(DbCommand cmd)
                               {
                                   var result = cmd.ExecuteScalar();
                                   var count = result is null ? 0 : Convert.ToInt32(result);
                               }
                           }
                           """);
    }
}

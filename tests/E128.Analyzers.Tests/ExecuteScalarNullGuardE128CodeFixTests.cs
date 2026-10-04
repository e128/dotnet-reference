using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ExecuteScalarNullGuardE128CodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<ExecuteScalarNullGuardAnalyzer, ExecuteScalarNullGuardCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConvertToInt32_AddsNullGuard()
    {
        const string source = """
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
                              """;

        const string fixedCode = """
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
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConvertToInt32_InsideABracelessIf_WrapsTheStatementInABlock()
    {
        const string source = """
                              using System;
                              using System.Data;
                              using System.Data.Common;

                              class C
                              {
                                  int? M(DbCommand cmd, bool enabled)
                                  {
                                      if (enabled)
                                          return {|E128042:Convert.ToInt32(cmd.ExecuteScalar())|};

                                      return null;
                                  }
                              }
                              """;

        const string fixedCode = """
                                 using System;
                                 using System.Data;
                                 using System.Data.Common;

                                 class C
                                 {
                                     int? M(DbCommand cmd, bool enabled)
                                     {
                                         if (enabled)
                                         {
                                             var result = cmd.ExecuteScalar();
                                             return result is null ? 0 : Convert.ToInt32(result);
                                         }

                                         return null;
                                     }
                                 }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }
}

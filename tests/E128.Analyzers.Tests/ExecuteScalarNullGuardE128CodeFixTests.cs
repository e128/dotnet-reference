using System.Threading.Tasks;
using E128.Analyzers.Design;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ExecuteScalarNullGuardE128CodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode) =>
        CodeFixTestRunner.RunAsync<ExecuteScalarNullGuardAnalyzer, ExecuteScalarNullGuardCodeFixProvider>(source, fixedCode);

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

    [Fact]
    [Trait("Category", "CI")]
    public Task ConvertToInt32_UsesAFreeName_WhenResultIsTaken()
    {
        const string source = """
                              using System;
                              using System.Data;
                              using System.Data.Common;

                              class C
                              {
                                  void M(DbCommand cmd, int result)
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
                                     void M(DbCommand cmd, int result)
                                     {
                                         var result2 = cmd.ExecuteScalar();
                                         var count = result2 is null ? 0 : Convert.ToInt32(result2);
                                     }
                                 }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConvertToInt32_OffersNoFix_WhenNoEnclosingStatementExists()
    {
        // A field initializer has no statement to declare the local in front of.
        return VerifyNoFixAsync("""
                                using System;
                                using System.Data.Common;

                                class C
                                {
                                    private static DbCommand Create() => throw new NotImplementedException();
                                    private readonly int _count = {|E128042:Convert.ToInt32(Create().ExecuteScalar())|};
                                }
                                """);
    }

    private static Task VerifyNoFixAsync(string code) =>
        CodeFixTestRunner.NoFixAsync<ExecuteScalarNullGuardAnalyzer, ExecuteScalarNullGuardCodeFixProvider>(code);
}

using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class PathCombineSegmentCodeFixTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_ReplacesCombineWithJoin()
    {
        const string source = """
                              using System.IO;
                              class C
                              {
                                  string M()
                                  {
                                      return Path.Combine("base", {|E128161:"/etc/passwd"|});
                                  }
                              }
                              """;

        const string fixedCode = """
                                 using System.IO;
                                 class C
                                 {
                                     string M()
                                     {
                                         return Path.Join("base", "/etc/passwd");
                                     }
                                 }
                                 """;

        return CodeFixTestRunner.RunAsync<PathCombineSegmentAnalyzer,
            PathCombineSegmentCodeFixProvider>(source, fixedCode);
    }
}

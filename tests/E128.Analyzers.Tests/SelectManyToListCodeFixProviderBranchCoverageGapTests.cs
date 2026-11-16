using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SelectManyToListCodeFixProviderBranchCoverageGapTests
{
    private static Task VerifyNoFixAsync(string source)
    {
        return CodeFixTestRunner.NoFixAsync<SelectManyToListAnalyzer, SelectManyToListCodeFixProvider>(source);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheSelectorIsAMethodGroup()
    {
        return VerifyNoFixAsync(
            """
            using System.Collections.Generic;
            using System.Linq;
            class C
            {
                static IEnumerable<int> Flatten(List<int> list) => list;

                void M(List<List<int>> lists)
                {
                    var flat = {|E128085:lists.SelectMany(Flatten).ToList()|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheSelectorIsAParenthesizedLambda()
    {
        return VerifyNoFixAsync(
            """
            using System.Collections.Generic;
            using System.Linq;
            class C
            {
                void M(List<List<int>> lists)
                {
                    var flat = {|E128085:lists.SelectMany((x) => x).ToList()|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheSelectorHasABlockBody()
    {
        return VerifyNoFixAsync(
            """
            using System.Collections.Generic;
            using System.Linq;
            class C
            {
                void M(List<List<int>> lists)
                {
                    var flat = {|E128085:lists.SelectMany(x => { return x; }).ToList()|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheDeclarationHoldsSeveralVariables()
    {
        return VerifyNoFixAsync(
            """
            using System.Collections.Generic;
            using System.Linq;
            class C
            {
                void M(List<List<int>> lists)
                {
                    List<int> flat = {|E128085:lists.SelectMany(x => x).ToList()|}, other = null;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheCallIsNotPartOfALocalDeclaration()
    {
        return VerifyNoFixAsync(
            """
            using System.Collections.Generic;
            using System.Linq;
            class C
            {
                List<int> M(List<List<int>> lists)
                {
                    return {|E128085:lists.SelectMany(x => x).ToList()|};
                }
            }
            """);
    }
}

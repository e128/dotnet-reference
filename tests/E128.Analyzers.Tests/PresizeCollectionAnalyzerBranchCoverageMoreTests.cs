using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class PresizeCollectionAnalyzerBranchCoverageMoreTests
{
    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<PresizeCollectionAnalyzer>(code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenOneDeclarationCreatesTwoCollections()
    {
        return VerifyAsync("""
            using System.Collections.Generic;
            class C
            {
                int M(IReadOnlyList<int> source)
                {
                    List<int> first = new(), second = new();
                    foreach (int item in source)
                    {
                        first.Add(item);
                    }

                    return first.Count + second.Count;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheDeclarationHasNoInitializer()
    {
        return VerifyAsync("""
            using System.Collections.Generic;
            class C
            {
                List<int> M(IReadOnlyList<int> source)
                {
                    List<int> result;
                    result = new();
                    foreach (int item in source)
                    {
                        result.Add(item);
                    }

                    return result;
                }
            }
            """);
    }
}

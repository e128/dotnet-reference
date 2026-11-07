using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class PresizeCollectionCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return CodeFixTestRunner.RunAsync<PresizeCollectionAnalyzer, PresizeCollectionCodeFixProvider>(
            source,
            fixedCode,
            markupOptions: MarkupOptions.None);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ImplicitCreation_GetsCapacity()
    {
        return VerifyFixAsync(
            """
            using System.Collections.Generic;
            class C
            {
                List<int> M(IReadOnlyList<int> source)
                {
                    List<int> result = {|E128205:new()|};
                    foreach (int item in source)
                    {
                        result.Add(item);
                    }

                    return result;
                }
            }
            """,
            """
            using System.Collections.Generic;
            class C
            {
                List<int> M(IReadOnlyList<int> source)
                {
                    List<int> result = new(source.Count);
                    foreach (int item in source)
                    {
                        result.Add(item);
                    }

                    return result;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ExplicitCreation_GetsCapacity()
    {
        return VerifyFixAsync(
            """
            using System.Collections.Generic;
            class C
            {
                List<int> M(int[] source)
                {
                    var result = {|E128205:new List<int>()|};
                    foreach (int item in source)
                    {
                        result.Add(item);
                    }

                    return result;
                }
            }
            """,
            """
            using System.Collections.Generic;
            class C
            {
                List<int> M(int[] source)
                {
                    var result = new List<int>(source.Length);
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

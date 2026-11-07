using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class EncodeInLoopCodeFixTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task GetBytesLength_ReplacedWithGetByteCount()
    {
        return CodeFixTestRunner.RunAsync<EncodeInLoopAnalyzer, EncodeInLoopCodeFixProvider>(
            """
            using System.Collections.Generic;
            using System.Text;
            class C
            {
                int M(IReadOnlyList<string> inputs)
                {
                    int total = 0;
                    foreach (string input in inputs)
                    {
                        total += {|E128209:Encoding.UTF8.GetBytes(input)|}.Length;
                    }

                    return total;
                }
            }
            """,
            """
            using System.Collections.Generic;
            using System.Text;
            class C
            {
                int M(IReadOnlyList<string> inputs)
                {
                    int total = 0;
                    foreach (string input in inputs)
                    {
                        total += Encoding.UTF8.GetByteCount(input);
                    }

                    return total;
                }
            }
            """);
    }
}

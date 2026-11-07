using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ToCharArrayCopyAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task ForeachOverToCharArray_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<ToCharArrayCopyAnalyzer>("""
            class C
            {
                int M(string value)
                {
                    int total = 0;
                    foreach (char c in {|E128212:value.ToCharArray()|})
                    {
                        total += c;
                    }

                    return total;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task IndexIntoToCharArray_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<ToCharArrayCopyAnalyzer>("""
            class C
            {
                char M(string value, int index)
                {
                    return {|E128212:value.ToCharArray()|}[index];
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AssignedToLocal_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<ToCharArrayCopyAnalyzer>("""
            class C
            {
                char[] M(string value)
                {
                    char[] chars = value.ToCharArray();
                    return chars;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PassedToMethod_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<ToCharArrayCopyAnalyzer>("""
            class C
            {
                void Consume(char[] chars)
                {
                }

                void M(string value)
                {
                    Consume(value.ToCharArray());
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CustomTypeToCharArray_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<ToCharArrayCopyAnalyzer>("""
            using System.Collections.Generic;
            class Buffer
            {
                public IReadOnlyList<char> ToCharArray() => new List<char>();
            }

            class C
            {
                void M(Buffer buffer)
                {
                    foreach (char c in buffer.ToCharArray())
                    {
                        _ = c;
                    }
                }
            }
            """);
    }
}

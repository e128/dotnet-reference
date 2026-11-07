using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class EncodeInLoopAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task GetBytesLength_InForeach_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<EncodeInLoopAnalyzer>("""
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
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task GetBytesLength_WithOtherEncoding_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<EncodeInLoopAnalyzer>("""
            using System.Collections.Generic;
            using System.Text;
            class C
            {
                int M(IReadOnlyList<string> inputs)
                {
                    int total = 0;
                    foreach (string input in inputs)
                    {
                        total += {|E128209:Encoding.Unicode.GetBytes(input)|}.Length;
                    }

                    return total;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task GetBytesLength_OutsideLoop_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<EncodeInLoopAnalyzer>("""
            using System.Text;
            class C
            {
                int M(string input)
                {
                    return Encoding.UTF8.GetBytes(input).Length;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task BytesUsedDirectly_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<EncodeInLoopAnalyzer>("""
            using System.Collections.Generic;
            using System.Text;
            class C
            {
                void M(IReadOnlyList<string> inputs)
                {
                    foreach (string input in inputs)
                    {
                        byte[] bytes = Encoding.UTF8.GetBytes(input);
                        bytes.ToString();
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task GetBytesLength_OnUnrelatedType_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<EncodeInLoopAnalyzer>("""
            using System.Collections.Generic;
            class Encoder
            {
                public byte[] GetBytes(string value) => new byte[value.Length];
            }

            class C
            {
                int M(IReadOnlyList<string> inputs, Encoder encoder)
                {
                    int total = 0;
                    foreach (string input in inputs)
                    {
                        total += encoder.GetBytes(input).Length;
                    }

                    return total;
                }
            }
            """);
    }
}

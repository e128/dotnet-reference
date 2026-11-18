using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SplitEnumerationCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return CodeFixTestRunner.RunAsync<SplitEnumerationAnalyzer, SplitEnumerationCodeFixProvider>(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheBodyParsesThePart()
    {
        return CodeFixTestRunner.NoFixAsync<SplitEnumerationAnalyzer, SplitEnumerationCodeFixProvider>(
            """
            using System;

            class C
            {
                int Sum(string line)
                {
                    int total = 0;
                    {|E128231:foreach (string part in line.Split(','))
                    {
                        total += int.Parse(part);
                    }|}
                    return total;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_ReadsTheLengthOfEachSlice()
    {
        return VerifyFixAsync(
            """
            using System;

            class C
            {
                int Count(string line)
                {
                    int total = 0;
                    {|E128231:foreach (string part in line.Split(", "))
                    {
                        total += part.Length;
                    }|}
                    return total;
                }
            }
            """,
            """
            using System;

            class C
            {
                int Count(string line)
                {
                    int total = 0;
                    foreach (var part in line.AsSpan().Split(", "))
                    {
                        total += line.AsSpan()[part].Length;
                    }
                    return total;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_ComparesEachSliceWithAString()
    {
        return VerifyFixAsync(
            """
            using System;

            class C
            {
                int Count(string line)
                {
                    int total = 0;
                    {|E128231:foreach (string part in line.Split(','))
                    {
                        if (part == "skip")
                        {
                            total++;
                        }
                    }|}
                    return total;
                }
            }
            """,
            """
            using System;

            class C
            {
                int Count(string line)
                {
                    int total = 0;
                    foreach (var part in line.AsSpan().Split(','))
                    {
                        if (line.AsSpan()[part] == "skip")
                        {
                            total++;
                        }
                    }
                    return total;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheBodyStoresThePartAsAString()
    {
        return CodeFixTestRunner.NoFixAsync<SplitEnumerationAnalyzer, SplitEnumerationCodeFixProvider>(
            """
            class C
            {
                void M(string line)
                {
                    {|E128231:foreach (string part in line.Split(','))
                    {
                        Handle(part);
                    }|}
                }

                static void Handle(string part)
                {
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheReceiverIsAMethodCall()
    {
        return CodeFixTestRunner.NoFixAsync<SplitEnumerationAnalyzer, SplitEnumerationCodeFixProvider>(
            """
            class C
            {
                void M()
                {
                    {|E128231:foreach (string part in GetLine().Split(','))
                    {
                        System.Console.WriteLine(int.Parse(part));
                    }|}
                }

                static string GetLine() => string.Empty;
            }
            """);
    }
}

using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class StringBuilderCapacityAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task ForeachAppend_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<StringBuilderCapacityAnalyzer>("""
            using System.Collections.Generic;
            using System.Text;
            class C
            {
                string M(IReadOnlyList<string> items)
                {
                    var builder = {|E128215:new StringBuilder()|};
                    foreach (string item in items)
                    {
                        builder.Append(item);
                    }

                    return builder.ToString();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ForAppendLine_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<StringBuilderCapacityAnalyzer>("""
            using System.Collections.Generic;
            using System.Text;
            class C
            {
                string M(IReadOnlyList<string> items)
                {
                    var builder = {|E128215:new StringBuilder()|};
                    for (int i = 0; i < items.Count; i++)
                    {
                        builder.AppendLine(items[i]);
                    }

                    return builder.ToString();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CapacityProvided_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<StringBuilderCapacityAnalyzer>("""
            using System.Collections.Generic;
            using System.Text;
            class C
            {
                string M(IReadOnlyList<string> items)
                {
                    var builder = new StringBuilder(64);
                    foreach (string item in items)
                    {
                        builder.Append(item);
                    }

                    return builder.ToString();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task InitialTextProvided_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<StringBuilderCapacityAnalyzer>("""
            using System.Collections.Generic;
            using System.Text;
            class C
            {
                string M(IReadOnlyList<string> items)
                {
                    var builder = new StringBuilder("prefix");
                    foreach (string item in items)
                    {
                        builder.Append(item);
                    }

                    return builder.ToString();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoLoop_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<StringBuilderCapacityAnalyzer>("""
            using System.Text;
            class C
            {
                string M()
                {
                    var builder = new StringBuilder();
                    builder.Append("value");
                    return builder.ToString();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LoopDoesNotAppendToBuilder_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<StringBuilderCapacityAnalyzer>("""
            using System.Collections.Generic;
            using System.Text;
            class C
            {
                string M(IReadOnlyList<string> items)
                {
                    var builder = new StringBuilder();
                    var copy = new List<string>();
                    foreach (string item in items)
                    {
                        copy.Add(item);
                    }

                    return builder.ToString();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LoopWithBreak_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<StringBuilderCapacityAnalyzer>("""
            using System.Collections.Generic;
            using System.Text;
            class C
            {
                string M(IReadOnlyList<string> items)
                {
                    var builder = new StringBuilder();
                    foreach (string item in items)
                    {
                        if (item.Length == 0)
                        {
                            break;
                        }

                        builder.Append(item);
                    }

                    return builder.ToString();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CapacitySetByInitializer_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<StringBuilderCapacityAnalyzer>("""
            using System.Collections.Generic;
            using System.Text;
            class C
            {
                string M(IReadOnlyList<string> items)
                {
                    var builder = new StringBuilder { Capacity = 64 };
                    foreach (string item in items)
                    {
                        builder.Append(item);
                    }

                    return builder.ToString();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AppendInsideLambda_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<StringBuilderCapacityAnalyzer>("""
            using System;
            using System.Collections.Generic;
            using System.Text;
            class C
            {
                string M(IReadOnlyList<string> items)
                {
                    var builder = new StringBuilder();
                    var callbacks = new List<Action>();
                    foreach (string item in items)
                    {
                        callbacks.Add(() => builder.Append(item));
                    }

                    return builder.ToString();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SwitchBreak_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<StringBuilderCapacityAnalyzer>("""
            using System.Collections.Generic;
            using System.Text;
            class C
            {
                string M(IReadOnlyList<string> items)
                {
                    var builder = {|E128215:new StringBuilder()|};
                    foreach (string item in items)
                    {
                        switch (item)
                        {
                            case "":
                                break;
                        }

                        builder.Append(item);
                    }

                    return builder.ToString();
                }
            }
            """);
    }
}

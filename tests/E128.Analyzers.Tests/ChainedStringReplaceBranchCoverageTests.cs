using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ChainedStringReplaceBranchCoverageTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return CodeFixTestRunner.RunAsync<ChainedStringReplaceAnalyzer, ChainedStringReplaceCodeFixProvider>(source, fixedCode);
    }

    private static Task VerifyNoFixAsync(string source)
    {
        return CodeFixTestRunner.NoFixAsync<ChainedStringReplaceAnalyzer, ChainedStringReplaceCodeFixProvider>(source);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Analyzer_ReportsOnlyTheOutermostCallOfALongerChain()
    {
        return AnalyzerTestRunner.VerifyAsync<ChainedStringReplaceAnalyzer>("""
            class C
            {
                void M(string value)
                {
                    var result = {|E128098:value.Replace('a', 'b').Replace('c', 'd').Replace('e', 'f')|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Analyzer_Silent_WhenAReplaceFollowsAnotherCall()
    {
        return AnalyzerTestRunner.VerifyAsync<ChainedStringReplaceAnalyzer>("""
            class C
            {
                void M(string value)
                {
                    var result = value.Trim().Replace('a', 'b');
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Analyzer_Silent_WhenAnotherTypeDefinesReplace()
    {
        return AnalyzerTestRunner.VerifyAsync<ChainedStringReplaceAnalyzer>("""
            class Editor
            {
                public Editor Replace(string search, string replacement)
                {
                    return this;
                }
            }

            class C
            {
                void M(Editor editor, string value)
                {
                    var result = editor.Replace("a", "b").Replace("c", "d");
                    var mixed = value.Replace("a", "b");
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Analyzer_ReportsAnUnbracedReplaceUntilStableLoop()
    {
        return AnalyzerTestRunner.VerifyAsync<ChainedStringReplaceAnalyzer>("""
            class C
            {
                void M(string value)
                {
                    {|E128098:while (value.Contains("--"))
                        value = value.Replace("--", "-");|}
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Analyzer_Silent_WhenTheWhileLoopIsNotAReplaceUntilStableLoop()
    {
        return AnalyzerTestRunner.VerifyAsync<ChainedStringReplaceAnalyzer>("""
            using System.Collections.Generic;
            class C
            {
                bool Contains(string text) => text.Length > 0;

                void M(string value, string other, bool flag, List<string> items)
                {
                    while (flag)
                    {
                        value = value.Replace("--", "-");
                    }

                    while (items.Contains("--"))
                    {
                        items[0] = items[0].Replace("--", "-");
                    }

                    while (Contains("--"))
                    {
                        value = value.Replace("--", "-");
                    }

                    while (value.Contains("--"))
                    {
                        other = value.Replace("--", "-");
                    }

                    while (value.Contains("--"))
                    {
                        value = other.Replace("--", "-");
                    }

                    while (value.Contains("--"))
                    {
                        value += "x";
                    }

                    while (value.Contains("--"))
                    {
                        value = value.Trim();
                    }

                    while (value.Contains("--"))
                    {
                        _ = value.Length;
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_CollapsesAnUnbracedLoopThatUsesTheShortOverloads()
    {
        return VerifyFixAsync(
            """
            class C
            {
                void M(string value)
                {
                    {|E128098:while (value.Contains("--"))
                        value = value.Replace("--", "-");|}
                }
            }
            """,
            """
            class C
            {
                void M(string value)
                {
                    {
                        var __buffer = new char[value.Length];
                        var __pos = 0;
                        var __collapsing = false;
                        foreach (var __c in value)
                        {
                            if (__c == '-')
                            {
                                if (!__collapsing)
                                {
                                    __buffer[__pos++] = __c;
                                    __collapsing = true;
                                }
                            }
                            else
                            {
                                __buffer[__pos++] = __c;
                                __collapsing = false;
                            }
                        }

                        value = new string(__buffer, 0, __pos);
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheLoopUsesNamesOrValuesTheFixCannotSee()
    {
        return VerifyNoFixAsync("""
            class C
            {
                            void M(string value, string search, string replacement, int count)
                {
                    {|E128098:while (value.Contains(search))
                    {
                        value = value.Replace("--", "-");
                    }|}

                    {|E128098:while (value.Contains("--"))
                    {
                        value = value.Replace(search, "-");
                    }|}

                    {|E128098:while (value.Contains("--"))
                    {
                        value = value.Replace("--", replacement);
                    }|}

                    {|E128098:while (value.Contains("--"))
                    {
                        value = value.Replace("--", "--");
                    }|}

                    {|E128098:while (value.Contains("--"))
                    {
                        value = value.Replace("xx", "x");
                    }|}

                    {|E128098:while (value.Contains("--"))
                    {
                        count = count + 1;
                        value = value.Replace("--", "-");
                    }|}
                }
            }
            """);
    }
}

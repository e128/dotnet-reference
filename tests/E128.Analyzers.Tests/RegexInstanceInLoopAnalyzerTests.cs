using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class RegexInstanceInLoopAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task PatternParameter_InForeach_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<RegexInstanceInLoopAnalyzer>("""
            using System.Collections.Generic;
            using System.Text.RegularExpressions;
            class C
            {
                void M(IReadOnlyList<string> inputs, string pattern)
                {
                    foreach (string input in inputs)
                    {
                        var regex = {|E128210:new Regex(pattern)|};
                        regex.IsMatch(input);
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task OptionsArgument_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<RegexInstanceInLoopAnalyzer>("""
            using System.Collections.Generic;
            using System.Text.RegularExpressions;
            class C
            {
                void M(IReadOnlyList<string> inputs, string pattern)
                {
                    foreach (string input in inputs)
                    {
                        var regex = {|E128210:new Regex(pattern, RegexOptions.Compiled)|};
                        regex.IsMatch(input);
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConstantPattern_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<RegexInstanceInLoopAnalyzer>("""
            using System.Collections.Generic;
            using System.Text.RegularExpressions;
            class C
            {
                void M(IReadOnlyList<string> inputs)
                {
                    foreach (string input in inputs)
                    {
                        var regex = {|E128210:new Regex("[0-9]+")|};
                        regex.IsMatch(input);
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PatternFromLoopVariable_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<RegexInstanceInLoopAnalyzer>("""
            using System.Collections.Generic;
            using System.Text.RegularExpressions;
            class C
            {
                void M(IReadOnlyList<string> patterns)
                {
                    foreach (string pattern in patterns)
                    {
                        var regex = new Regex(pattern);
                        regex.IsMatch(pattern);
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task OutsideLoop_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<RegexInstanceInLoopAnalyzer>("""
            using System.Text.RegularExpressions;
            class C
            {
                Regex M(string pattern)
                {
                    return new Regex(pattern);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task WhileLoopCallPattern_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<RegexInstanceInLoopAnalyzer>("""
            using System.Text.RegularExpressions;
            class Reader
            {
                public bool Read() => false;
                public string GetString(int index) => string.Empty;
            }

            class C
            {
                void M(Reader reader)
                {
                    while (reader.Read())
                    {
                        var regex = new Regex(reader.GetString(0));
                        regex.IsMatch("x");
                    }
                }
            }
            """);
    }
}

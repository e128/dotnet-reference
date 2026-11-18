using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SplitEnumerationAnalyzerTests
{
    private static DiagnosticResult Expected()
    {
        return new DiagnosticResult("E128231", DiagnosticSeverity.Warning).WithLocation(0);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAForeachEnumeratesACharSplit()
    {
        return AnalyzerTestRunner.VerifyAsync<SplitEnumerationAnalyzer>(
            """
            class C
            {
                int Sum(string line)
                {
                    int total = 0;
                    {|#0:foreach (string part in line.Split(','))
                    {
                        total += int.Parse(part);
                    }|}
                    return total;
                }
            }
            """,
            Expected());
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheLoopVariableIsImplicit()
    {
        return AnalyzerTestRunner.VerifyAsync<SplitEnumerationAnalyzer>(
            """
            class C
            {
                void M(string line)
                {
                    {|#0:foreach (var part in line.Split(','))
                    {
                        System.Console.WriteLine(part);
                    }|}
                }
            }
            """,
            Expected());
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheSeparatorIsAConstantString()
    {
        return AnalyzerTestRunner.VerifyAsync<SplitEnumerationAnalyzer>(
            """
            class C
            {
                int Count(string line)
                {
                    int total = 0;
                    {|#0:foreach (string part in line.Split(", "))
                    {
                        total += part.Length;
                    }|}
                    return total;
                }
            }
            """,
            Expected());
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCallPassesStringSplitOptions()
    {
        return AnalyzerTestRunner.VerifyAsync<SplitEnumerationAnalyzer>(
            """
            using System;

            class C
            {
                int Sum(string line)
                {
                    int total = 0;
                    foreach (string part in line.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        total += int.Parse(part);
                    }
                    return total;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheSeparatorIsNotConstant()
    {
        return AnalyzerTestRunner.VerifyAsync<SplitEnumerationAnalyzer>(
            """
            class C
            {
                int Sum(string line, string separator)
                {
                    int total = 0;
                    foreach (string part in line.Split(separator))
                    {
                        total += int.Parse(part);
                    }
                    return total;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLoopEnumeratesSomethingElse()
    {
        return AnalyzerTestRunner.VerifyAsync<SplitEnumerationAnalyzer>(
            """
            class C
            {
                int Sum(string[] parts)
                {
                    int total = 0;
                    foreach (string part in parts)
                    {
                        total += int.Parse(part);
                    }
                    return total;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void Silent_WhenTheCoreTypesAreMissing()
    {
        var compilation = CSharpCompilation.Create("Gate");

        Assert.Null(SplitEnumerationAnalyzer.CreateTerms(compilation));
    }
}

using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SubstringLengthUnderflowAnalyzerBranchCoverageMoreTests
{
    private static AnalyzerTestOptions Lenient => new() { CompilerDiagnostics = CompilerDiagnostics.None };

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheSubstringCallDoesNotResolve()
    {
        return AnalyzerTestRunner.VerifyAsync<SubstringLengthUnderflowAnalyzer>(
            """
            class C
            {
                string M(Missing s) => s.Substring(0, s.Length - 1);
            }
            """,
            options: Lenient);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheCallStartsItsOwnStatement()
    {
        return AnalyzerTestRunner.VerifyAsync<SubstringLengthUnderflowAnalyzer>("""
            class C
            {
                void M(string s)
                {
                    {|E128198:s.Substring(0, s.Length - 1)|}.Trim();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheConditionOnlyCallsUnrelatedMethods()
    {
        return AnalyzerTestRunner.VerifyAsync<SubstringLengthUnderflowAnalyzer>(
            """
            using System.Collections.Generic;

            class C
            {
                static bool Check() => true;

                string M(string s, List<int> list)
                {
                    if (Check() || s.ToUpper() == string.Empty || list.Contains(1) || Missing.Probe(2))
                    {
                        return {|E128198:s.Substring(0, s.Length - 1)|};
                    }

                    return s;
                }
            }
            """,
            options: Lenient);
    }
}

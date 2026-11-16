using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SearchValuesHoistingAnalyzerBranchCoverageMoreTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCompilationHasNoSearchValues()
    {
        return AnalyzerTestRunner.VerifyAsync<SearchValuesHoistingAnalyzer>(
            """
            class Factory
            {
                public static int Create(string values) => values.Length;
            }

            class C
            {
                int M()
                {
                    return Factory.Create("abc");
                }
            }
            """,
            options: new AnalyzerTestOptions { References = ReferenceAssemblies.NetStandard.NetStandard20 });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenCreateIsADynamicCallOrAnotherType()
    {
        return AnalyzerTestRunner.VerifyAsync<SearchValuesHoistingAnalyzer>("""
            using System.Buffers;
            class Factory
            {
                public static int Create(string values) => values.Length;
            }

            class C
            {
                void M()
                {
                    dynamic factory = new Factory();
                    factory.Create("abc");
                    _ = Factory.Create("abc");
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheArgumentIsAStaticFieldThatIsNotReadonly()
    {
        return AnalyzerTestRunner.VerifyAsync<SearchValuesHoistingAnalyzer>("""
            using System;
            using System.Buffers;
            class C
            {
                private static string chars = "abc";

                bool M(ReadOnlySpan<char> text)
                {
                    return text.ContainsAny(SearchValues.Create(chars));
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheArgumentCallsAStaticMethod()
    {
        return AnalyzerTestRunner.VerifyAsync<SearchValuesHoistingAnalyzer>("""
            using System;
            using System.Buffers;
            class C
            {
                private static string Chars() => "abc";

                bool M(ReadOnlySpan<char> text)
                {
                    return text.ContainsAny({|E128124:SearchValues.Create(Chars())|});
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheArgumentMixesAConstantAndAStaticReadonlyField()
    {
        return AnalyzerTestRunner.VerifyAsync<SearchValuesHoistingAnalyzer>("""
            using System;
            using System.Buffers;
            class C
            {
                private const string Prefix = "a";
                private static readonly string Suffix = "b";

                bool M(ReadOnlySpan<char> text)
                {
                    return text.ContainsAny({|E128124:SearchValues.Create(Prefix + Suffix)|});
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheTableIsAnInstancePropertyInitializer()
    {
        return AnalyzerTestRunner.VerifyAsync<SearchValuesHoistingAnalyzer>("""
            using System.Buffers;
            class C
            {
                SearchValues<char> Values { get; } = {|E128124:SearchValues.Create("abc")|};
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheTableIsAStaticExpressionBodiedProperty()
    {
        return AnalyzerTestRunner.VerifyAsync<SearchValuesHoistingAnalyzer>("""
            using System.Buffers;
            class C
            {
                static SearchValues<char> Values => {|E128124:SearchValues.Create("abc")|};
            }
            """);
    }
}

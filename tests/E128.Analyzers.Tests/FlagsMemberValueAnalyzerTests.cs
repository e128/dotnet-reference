using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FlagsMemberValueAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAnUnnamedCombination()
    {
        return AnalyzerTestRunner.VerifyAsync<FlagsMemberValueAnalyzer>("""
            [System.Flags]
            enum Permission
            {
                None = 0,
                Read = 1,
                Write = 2,
                {|E128246:Execute|} = 3
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForACombinationWithAnExtraBit()
    {
        return AnalyzerTestRunner.VerifyAsync<FlagsMemberValueAnalyzer>("""
            [System.Flags]
            enum Permission
            {
                None = 0,
                Read = 1,
                Write = 2,
                Execute = 4,
                {|E128246:ReadWrite|} = 7
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForANamedCombination()
    {
        return AnalyzerTestRunner.VerifyAsync<FlagsMemberValueAnalyzer>("""
            [System.Flags]
            enum Permission
            {
                None = 0,
                Read = 1,
                Write = 2,
                ReadWrite = 3
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAnAllMember()
    {
        return AnalyzerTestRunner.VerifyAsync<FlagsMemberValueAnalyzer>("""
            [System.Flags]
            enum Permission
            {
                None = 0,
                Read = 1,
                Write = 2,
                All = 3
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForSingleBitMembers()
    {
        return AnalyzerTestRunner.VerifyAsync<FlagsMemberValueAnalyzer>("""
            [System.Flags]
            enum Permission
            {
                None = 0,
                Read = 1,
                Write = 2,
                Execute = 4
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForANegativeAllBitsMember()
    {
        return AnalyzerTestRunner.VerifyAsync<FlagsMemberValueAnalyzer>("""
            [System.Flags]
            enum Permission
            {
                None = 0,
                Read = 1,
                All = -1
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForANonFlagsEnum()
    {
        return AnalyzerTestRunner.VerifyAsync<FlagsMemberValueAnalyzer>("""
            enum Permission
            {
                None = 0,
                Read = 1,
                Write = 2,
                Execute = 3
            }
            """);
    }
}

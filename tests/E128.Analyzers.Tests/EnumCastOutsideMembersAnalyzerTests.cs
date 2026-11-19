using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class EnumCastOutsideMembersAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAConstantOutsideTheMemberSet()
    {
        return AnalyzerTestRunner.VerifyAsync<EnumCastOutsideMembersAnalyzer>("""
            enum Status
            {
                None = 0,
                Ready = 1,
                Busy = 2
            }

            class C
            {
                Status M() => {|E128245:(Status)7|};
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForANegativeValueOutsideTheMemberSet()
    {
        return AnalyzerTestRunner.VerifyAsync<EnumCastOutsideMembersAnalyzer>("""
            enum Status
            {
                None = 0,
                Ready = 1,
                Busy = 2
            }

            class C
            {
                Status M() => {|E128245:(Status)(-1)|};
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForADeclaredMemberValue()
    {
        return AnalyzerTestRunner.VerifyAsync<EnumCastOutsideMembersAnalyzer>("""
            enum Status
            {
                None = 0,
                Ready = 1,
                Busy = 2
            }

            class C
            {
                Status M() => (Status)2;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAConstantFieldWithADeclaredValue()
    {
        return AnalyzerTestRunner.VerifyAsync<EnumCastOutsideMembersAnalyzer>("""
            enum Status
            {
                None = 0,
                Ready = 1,
                Busy = 2
            }

            class C
            {
                private const int Busy = 2;

                Status M() => (Status)Busy;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAVariableOperand()
    {
        return AnalyzerTestRunner.VerifyAsync<EnumCastOutsideMembersAnalyzer>("""
            enum Status
            {
                None = 0,
                Ready = 1
            }

            class C
            {
                Status M(int value) => (Status)value;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAnAliasValue()
    {
        return AnalyzerTestRunner.VerifyAsync<EnumCastOutsideMembersAnalyzer>("""
            enum Status
            {
                None = 0,
                Ready = 1,
                Busy = 2,
                Working = 2
            }

            class C
            {
                Status M() => (Status)2;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAFlagsEnum()
    {
        return AnalyzerTestRunner.VerifyAsync<EnumCastOutsideMembersAnalyzer>("""
            [System.Flags]
            enum Permission
            {
                None = 0,
                Read = 1,
                Write = 2
            }

            class C
            {
                Permission M() => (Permission)5;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForANonEnumCast()
    {
        return AnalyzerTestRunner.VerifyAsync<EnumCastOutsideMembersAnalyzer>("""
            class C
            {
                long M() => (long)7;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_InATestCompilation()
    {
        return AnalyzerTestRunner.VerifyAsync<EnumCastOutsideMembersAnalyzer>(
            """
            using Xunit;

            enum Status
            {
                None = 0,
                Ready = 1,
                Busy = 2
            }

            class T
            {
                [Fact]
                public void A() => _ = (Status)7;
            }
            """,
            options: new AnalyzerTestOptions { References = TestCompilation });
    }

    private static readonly ReferenceAssemblies TestCompilation = ReferenceAssemblies.Net.Net100
        .AddPackages([
            new PackageIdentity("xunit.v3.core", "3.2.2"),
            new PackageIdentity("xunit.v3.assert", "3.2.2")
        ]);
}

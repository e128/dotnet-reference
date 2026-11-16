using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class LazyFieldThreadSafetyAnalyzerBranchCoverageGapTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheNamedModeFollowsTheNamedFactory()
    {
        return AnalyzerTestRunner.VerifyAsync<LazyFieldThreadSafetyAnalyzer>(
            """
            using System;
            using System.Threading;

            class C
            {
                private static readonly Lazy<int> {|E128194:Value = new Lazy<int>(valueFactory: () => 1, mode: LazyThreadSafetyMode.None)|};
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAnotherModeTypeWithoutANoneMemberIsDeclared()
    {
        return AnalyzerTestRunner.VerifyAsync<LazyFieldThreadSafetyAnalyzer>(
            """
            using System;

            namespace System.Threading
            {
                public enum LazyThreadSafetyMode { Other = 7 }
            }

            class C
            {
                private static readonly Lazy<int> {|E128194:Value = new Lazy<int>(() => 1, false)|};
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAnotherModeTypeHasANonConstantNoneField()
    {
        return AnalyzerTestRunner.VerifyAsync<LazyFieldThreadSafetyAnalyzer>(
            """
            using System;

            namespace System.Threading
            {
                public sealed class LazyThreadSafetyMode
                {
                    public static readonly int None = 0;
                }
            }

            class C
            {
                private static readonly Lazy<int> {|E128194:Value = new Lazy<int>(() => 1, false)|};
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAnotherModeTypeHasALongNoneConstant()
    {
        return AnalyzerTestRunner.VerifyAsync<LazyFieldThreadSafetyAnalyzer>(
            """
            using System;

            namespace System.Threading
            {
                public enum LazyThreadSafetyMode : long { None = 0 }
            }

            class C
            {
                private static readonly Lazy<int> {|E128194:Value = new Lazy<int>(() => 1, false)|};
            }
            """);
    }
}

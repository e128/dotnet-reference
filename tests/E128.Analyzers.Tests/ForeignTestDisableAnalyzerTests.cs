using System.Threading.Tasks;
using E128.Analyzers.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ForeignTestDisableAnalyzerTests
{
    private const string NUnitPreamble = """
        using NUnit.Framework;

        namespace NUnit.Framework
        {
            using System;

            public sealed class TestAttribute : Attribute
            {
            }

            public sealed class IgnoreAttribute : Attribute
            {
                public IgnoreAttribute()
                {
                }

                public IgnoreAttribute(string reason)
                {
                }
            }
        }
        """;

    private const string MsTestPreamble = """
        using Microsoft.VisualStudio.TestTools.UnitTesting;

        namespace Microsoft.VisualStudio.TestTools.UnitTesting
        {
            using System;

            public sealed class TestMethodAttribute : Attribute
            {
            }

            public sealed class IgnoreAttribute : Attribute
            {
            }
        }
        """;

    private static AnalyzerTestOptions Options => new() { References = Net100WithXunit };

    private static readonly ReferenceAssemblies Net100WithXunit = ReferenceAssemblies.Net.Net100
        .AddPackages([
            new PackageIdentity("xunit.v3.core", "3.2.2"),
            new PackageIdentity("xunit.v3.assert", "3.2.2")
        ]);

    [Fact]
    [Trait("Category", "CI")]
    public Task NUnitIgnoreWithUntrackedReason_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<ForeignTestDisableAnalyzer>(
            NUnitPreamble + """

                        public class T
                        {
                            [Test]
                            [{|E128237:Ignore("flaky on CI")|}]
                            public void A() { }
                        }
                        """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task MsTestIgnoreWithoutReason_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<ForeignTestDisableAnalyzer>(
            MsTestPreamble + """

                        public class T
                        {
                            [TestMethod]
                            [{|E128237:Ignore|}]
                            public void A() { }
                        }
                        """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TestMethodInsideIfFalse_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<ForeignTestDisableAnalyzer>("""
                           public class T
                           {
                           #if false
                               {|E128237:[Fact]
                               public void A() { }|}
                           #endif
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task EachTestInsideOneIfFalseRegionReports()
    {
        return AnalyzerTestRunner.VerifyAsync<ForeignTestDisableAnalyzer>("""
                           public class T
                           {
                           #if false
                               {|E128237:[Fact]
                               public void A() { }|}

                               {|E128237:[Theory]
                               public void B(int x) { }|}
                           #endif
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TrackedNUnitIgnoreReason_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<ForeignTestDisableAnalyzer>(
            NUnitPreamble + """

                        public class T
                        {
                            [Test]
                            [Ignore("AB#4521 flaky on CI")]
                            public void A() { }
                        }
                        """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task IgnoreWithoutTestMethod_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<ForeignTestDisableAnalyzer>(
            NUnitPreamble + """

                        public class T
                        {
                            [Ignore("later")]
                            public void A() { }
                        }
                        """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task XunitSkipArgument_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<ForeignTestDisableAnalyzer>("""
                           using Xunit;

                           public class T
                           {
                               [Fact(Skip = "AB#4521 flaky on CI")]
                               public void A() { }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TestInsideIfTrue_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<ForeignTestDisableAnalyzer>("""
                           using Xunit;

                           public class T
                           {
                           #if true
                               [Fact]
                               public void A() { }
                           #endif
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TestInsideIfDebug_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<ForeignTestDisableAnalyzer>("""
                           public class T
                           {
                           #if DEBUG
                               [Fact]
                               public void A() { }
                           #endif
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TestInsideElseBranch_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<ForeignTestDisableAnalyzer>("""
                           using Xunit;

                           public class T
                           {
                           #if false
                           #else
                               [Fact]
                               public void A() { }
                           #endif
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TestInsideElifFalse_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<ForeignTestDisableAnalyzer>("""
                           public class T
                           {
                           #if DEBUG
                           #elif false
                               [Fact]
                               public void A() { }
                           #endif
                           }
                           """);
    }
}

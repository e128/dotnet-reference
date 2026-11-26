using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;
using static E128.Analyzers.Tests.ComplexityCeilingTestHarness;

namespace E128.Analyzers.Tests;

public sealed class ComplexityCeilingCodeFixBranchCoverageTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Membership_UsesTheOuterSideWhenItIsOnTheRight()
    {
        return VerifyMembershipAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            class C
            {
                bool Has(string[] items, string[] expected)
                {
                    foreach (var a in items)
                    {
                        foreach (var b in expected)
                        {
                            if (b == a)
                            {
                                return true;
                            }
                        }
                    }

                    return false;
                }
            }
            """,
            """
            using System.Collections.Generic;
            using System.Linq;

            class C
            {
                bool Has(string[] items, string[] expected)
                {
                    var expectedSet = expected.ToHashSet();
                    foreach (var a in items)
                    {
                        if (expectedSet.Contains(a))
                        {
                            return true;
                        }
                    }

                    return false;
                }
            }
            """,
            Warning(10, 13));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Membership_UsesAMemberOfTheOuterElement()
    {
        return VerifyMembershipAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            class Item
            {
                public string Name = "";
            }

            class C
            {
                bool Has(Item[] items, string[] names)
                {
                    foreach (var item in items)
                    {
                        foreach (var name in names)
                        {
                            if (item.Name == name)
                            {
                                return true;
                            }
                        }
                    }

                    return false;
                }
            }
            """,
            """
            using System.Collections.Generic;
            using System.Linq;

            class Item
            {
                public string Name = "";
            }

            class C
            {
                bool Has(Item[] items, string[] names)
                {
                    var namesSet = names.ToHashSet();
                    foreach (var item in items)
                    {
                        if (namesSet.Contains(item.Name))
                        {
                            return true;
                        }
                    }

                    return false;
                }
            }
            """,
            Warning(15, 13));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Membership_UsesAnElementOfTheOuterElement()
    {
        return VerifyMembershipAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            class C
            {
                bool Has(string[][] rows, string[] names)
                {
                    foreach (var row in rows)
                    {
                        foreach (var name in names)
                        {
                            if ((row[0]) == name)
                            {
                                return true;
                            }
                        }
                    }

                    return false;
                }
            }
            """,
            """
            using System.Collections.Generic;
            using System.Linq;

            class C
            {
                bool Has(string[][] rows, string[] names)
                {
                    var namesSet = names.ToHashSet();
                    foreach (var row in rows)
                    {
                        if (namesSet.Contains((row[0])))
                        {
                            return true;
                        }
                    }

                    return false;
                }
            }
            """,
            Warning(10, 13));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Membership_HandlesAnEmbeddedReturnInTheGuard()
    {
        return VerifyMembershipAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            class C
            {
                bool Has(int[] items, int[] expected)
                {
                    foreach (var a in items)
                    {
                        foreach (var b in expected)
                        {
                            if (a == b) return true;
                        }
                    }

                    return false;
                }
            }
            """,
            """
            using System.Collections.Generic;
            using System.Linq;

            class C
            {
                bool Has(int[] items, int[] expected)
                {
                    var expectedSet = expected.ToHashSet();
                    foreach (var a in items)
                    {
                        if (expectedSet.Contains(a)) return true;
                    }

                    return false;
                }
            }
            """,
            Warning(10, 13));
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("string[] items, string[] expected, string key", "expected", "{ if (key == b) { return true; } }", "")]
    [InlineData("string[] items, string[] expected", "expected", "{ if (a == b) { return true; } else { continue; } }", "")]
    [InlineData("string[] items, string[] expected", "expected", "{ if (a != b) { return true; } }", "")]
    [InlineData("string[] items, string[] expected", "expected", "{ if (a == b) { items = expected; return true; } }", "")]
    [InlineData("string[] items, string[] expected", "expected", "if (a == b) { return true; }", "")]
    [InlineData("string[] items", "_field", "{ if (a == b) { return true; } }", "private readonly string[] _field = new string[2];")]
    [InlineData("string[] items", "new List<string>()", "{ if (a == b) { return true; } }", "")]
    [InlineData("string[] items, string[] expected", "expected", "{ if (Next(a) == b) { return true; } }", "static string Next(string value) => value;")]
    [InlineData("string[] items, string[] expected", "expected", "{ if (a == b + \"x\") { return true; } }", "")]
    [InlineData("object[] items, object[] expected", "expected", "{ if (a == b) { return true; } }", "")]
    [InlineData("Money[] items, Money[] expected", "expected", "{ if (a == b) { return true; } }", "class Money { public static bool operator ==(Money l, Money r) => true; public static bool operator !=(Money l, Money r) => false; public override bool Equals(object? o) => true; public override int GetHashCode() => 0; }")]
    public Task Membership_NoAction_WhenTheScanIsNotASafeSetLookup(
        string parameters,
        string innerSource,
        string innerBody,
        string members)
    {
        var source = $$"""
            using System.Collections.Generic;

            class C
            {
                bool Has({{parameters}})
                {
                    foreach (var a in items)
                    {
                        foreach (var b in {{innerSource}})
                        {{innerBody}}
                    }

                    return false;
                }

                {{members}}
            }
            """;

        return VerifyNoMembershipAsync(source, Warning(9, 13));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Membership_FixesTheScanInsideASwitchSection()
    {
        return VerifyMembershipAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            class C
            {
                bool Has(int mode, string[] items, string[] expected)
                {
                    switch (mode)
                    {
                        case 1:
                            foreach (var a in items)
                            {
                                foreach (var b in expected)
                                {
                                    if (a == b)
                                    {
                                        return true;
                                    }
                                }
                            }

                            break;
                    }

                    return false;
                }
            }
            """,
            """
            using System.Collections.Generic;
            using System.Linq;

            class C
            {
                bool Has(int mode, string[] items, string[] expected)
                {
                    switch (mode)
                    {
                        case 1:
                            var expectedSet = expected.ToHashSet();
                            foreach (var a in items)
                            {
                                if (expectedSet.Contains(a))
                                {
                                    return true;
                                }
                            }

                            break;
                    }

                    return false;
                }
            }
            """,
            Warning(13, 21));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Suppression_NoAction_WhenTheLoopIsAnEmbeddedStatement()
    {
        return CodeFixTestRunner.NoFixAsync<ComplexityCeilingAnalyzer, ComplexityCeilingSuppressionCodeFixProvider>(
            """
            class C
            {
                void M(string[] items, bool flag)
                {
                    foreach (var a in items)
                        if (flag)
                            foreach (var b in items)
                            {
                                _ = a.Length + b.Length;
                            }
                }
            }
            """,
            expected: Warning(7, 17));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Suppression_WrapsALoopInASwitchSection()
    {
        return VerifySuppressionAsync(
            """
            class C
            {
                void M(int mode, string[] items)
                {
                    switch (mode)
                    {
                        case 1:
                            foreach (var a in items)
                            {
                                foreach (var b in items)
                                {
                                    _ = a.Length + b.Length;
                                }
                            }

                            break;
                    }
                }
            }
            """,
            """
            class C
            {
                void M(int mode, string[] items)
                {
                    switch (mode)
                    {
                        case 1:
                            foreach (var a in items)
                            {
                                #pragma warning disable E128152 // Justification: state why this site is output-bound
                                foreach (var b in items)
                                {
                                    _ = a.Length + b.Length;
                                }
                                #pragma warning restore E128152
                            }

                            break;
                    }
                }
            }
            """,
            Warning(10, 21));
    }
}

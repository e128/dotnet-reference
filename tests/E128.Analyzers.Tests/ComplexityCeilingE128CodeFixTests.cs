using System.Threading.Tasks;
using E128.Analyzers.Performance;
using E128.Analyzers.Style;
using Xunit;
using static E128.Analyzers.Tests.ComplexityCeilingTestHarness;

namespace E128.Analyzers.Tests;

public sealed class ComplexityCeilingE128CodeFixTests
{
    private const string TwoSiteSource = """
                                         using System.Collections.Generic;
                                         using System.Linq;

                                         class C
                                         {
                                             bool HasA(string[] items, string[] expected)
                                             {
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

                                                 return false;
                                             }

                                             bool HasB(string[] items, string[] expected)
                                             {
                                                 foreach (var c in items)
                                                 {
                                                     foreach (var d in expected)
                                                     {
                                                         if (c == d)
                                                         {
                                                             return true;
                                                         }
                                                     }
                                                 }

                                                 return false;
                                             }
                                         }
                                         """;

    private const string TwoSiteFixed = """
                                        using System.Collections.Generic;
                                        using System.Linq;

                                        class C
                                        {
                                            bool HasA(string[] items, string[] expected)
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

                                            bool HasB(string[] items, string[] expected)
                                            {
                                                var expectedSet = expected.ToHashSet();
                                                foreach (var c in items)
                                                {
                                                    if (expectedSet.Contains(c))
                                                    {
                                                        return true;
                                                    }
                                                }

                                                return false;
                                            }
                                        }
                                        """;

    [Fact]
    [Trait("Category", "CI")]
    public Task ComplexityCeilingMembershipScanCodeFix_OffersNoAction_WhenOuterLoopIsItselfNested()
    {
        return VerifyNoMembershipAsync(
            """
            class C
            {
                bool Has(string[] groups, string[] items, string[] expected)
                {
                    foreach (var group in groups)
                    {
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
                    }

                    return false;
                }
            }
            """,
            Warning(7, 13),
            Warning(9, 17));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ComplexityCeilingSuppressionCodeFix_WrapsLoop_InJustifiedPragmaPair()
    {
        return VerifySuppressionAsync(
            """
            class C
            {
                void M(string[] items)
                {
                    foreach (var a in items)
                    {
                        foreach (var b in items)
                        {
                            _ = a.Length + b.Length;
                        }
                    }
                }
            }
            """,
            """
            class C
            {
                void M(string[] items)
                {
                    foreach (var a in items)
                    {
                        #pragma warning disable E128152 // Justification: state why this site is output-bound
                        foreach (var b in items)
                        {
                            _ = a.Length + b.Length;
                        }
                        #pragma warning restore E128152
                    }
                }
            }
            """,
            Warning(7, 13));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ComplexityCeilingSuppressionCodeFix_WrapsMethodDeclaration_ForRecursionDiagnostic()
    {
        return VerifySuppressionAsync(
            """
            class C
            {
                int Fib(int n)
                {
                    if (n < 2)
                    {
                        return n;
                    }

                    return Fib(n - 1) + Fib(n - 2);
                }
            }
            """,
            """
            class C
            {
                #pragma warning disable E128152 // Justification: state why this site is output-bound
                int Fib(int n)
                {
                    if (n < 2)
                    {
                        return n;
                    }

                    return Fib(n - 1) + Fib(n - 2);
                }
                #pragma warning restore E128152
            }
            """,
            Warning(3, 5));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ComplexityCeilingMembershipScanCodeFix_OffersNoAction_WhenReturnReadsInnerVariable()
    {
        return VerifyNoMembershipAsync(
            """
            class C
            {
                string Match(string[] items, string[] expected)
                {
                    foreach (var a in items)
                    {
                        foreach (var b in expected)
                        {
                            if (a == b)
                            {
                                return b;
                            }
                        }
                    }

                    return string.Empty;
                }
            }
            """,
            Warning(7, 13));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ComplexityCeilingMembershipScanCodeFix_OffersNoAction_WhenOuterLoopIsEmbedded()
    {
        return VerifyNoMembershipAsync(
            """
            class C
            {
                bool Has(string[] items, string[] expected)
                {
                    if (items.Length > 0)
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

                    return false;
                }
            }
            """,
            Warning(8, 17));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ComplexityCeilingMembershipScanCodeFix_OffersNoAction_WhenElementsAreFloatingPoint()
    {
        return VerifyNoMembershipAsync(
            """
            class C
            {
                bool Has(double[] items, double[] expected)
                {
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

                    return false;
                }
            }
            """,
            Warning(7, 13));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ComplexityCeilingMembershipScanCodeFix_KeepCommentsAboveBothLoops()
    {
        return VerifyMembershipAsync(
            """
            using System.Linq;

            class C
            {
                bool Has(string[] items, string[] expected)
                {
                    // Scan every item against the expected list.
                    foreach (var a in items)
                    {
                        foreach (var b in expected)
                        {
                            // Exact match only.
                            if (a == b)
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
            using System.Linq;

            class C
            {
                bool Has(string[] items, string[] expected)
                {
                    var expectedSet = expected.ToHashSet();
                    // Scan every item against the expected list.
                    foreach (var a in items)
                    {
                        // Exact match only.
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
    public Task ComplexityCeilingMembershipScanCodeFix_RenamesTheSet_WhenTheNameIsTaken()
    {
        return VerifyMembershipAsync(
            """
            using System.Linq;

            class C
            {
                bool Has(string[] items, string[] expected, int expectedSet)
                {
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

                    return false;
                }
            }
            """,
            """
            using System.Linq;

            class C
            {
                bool Has(string[] items, string[] expected, int expectedSet)
                {
                    var expectedSet2 = expected.ToHashSet();
                    foreach (var a in items)
                    {
                        if (expectedSet2.Contains(a))
                        {
                            return true;
                        }
                    }

                    return false;
                }
            }
            """,
            Warning(9, 13));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ComplexityCeilingSuppressionCodeFix_EmitsReason_E128047Accepts()
    {
        return AnalyzerTestRunner.VerifyAsync<SuppressionAuditAnalyzer>(
            $$"""
            class C
            {
                void M(string[] items)
                {
                    {{ComplexityCeilingSuppressionCodeFixProvider.DisableDirective}}
                    foreach (var a in items)
                    {
                        foreach (var b in items)
                        {
                            _ = a.Length + b.Length;
                        }
                    }
                    {{ComplexityCeilingSuppressionCodeFixProvider.RestoreDirective}}
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ComplexityCeilingMembershipScanCodeFix_ReplacesNestedScan_WithSetLookup()
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
                            if (a == b)
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
    public Task ComplexityCeilingMembershipScanCodeFix_OffersNoAction_WhenInnerBodyDoesMoreThanOneTest()
    {
        return VerifyNoMembershipAsync(
            """
            class C
            {
                bool Has(string[] items, string[] expected)
                {
                    foreach (var a in items)
                    {
                        foreach (var b in expected)
                        {
                            if (a == b)
                            {
                                return true;
                            }

                            if (b == "x")
                            {
                                return false;
                            }
                        }
                    }

                    return false;
                }
            }
            """,
            Warning(7, 13));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ComplexityCeilingMembershipScanCodeFix_OffersNoAction_WhenGuardBodyIsBreak()
    {
        return VerifyNoMembershipAsync(
            """
            class C
            {
                void M(string[] items, string[] expected)
                {
                    foreach (var a in items)
                    {
                        foreach (var b in expected)
                        {
                            if (a == b)
                            {
                                break;
                            }
                        }
                    }
                }
            }
            """,
            Warning(7, 13));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ComplexityCeilingMembershipScanCodeFix_OffersNoAction_WhenInnerCollectionIsReassigned()
    {
        return VerifyNoMembershipAsync(
            """
            class C
            {
                void M(string[] items)
                {
                    var expected = new[] { "x" };
                    foreach (var a in items)
                    {
                        foreach (var b in expected)
                        {
                            if (a == b)
                            {
                                return;
                            }
                        }
                    }

                    expected = new[] { "y" };
                }
            }
            """,
            Warning(8, 13));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ComplexityCeilingMembershipScanCodeFix_OffersNoAction_WhenReportedNodeIsRecursiveMethod()
    {
        return VerifyNoMembershipAsync(
            """
            class C
            {
                int Fib(int n)
                {
                    if (n < 2)
                    {
                        return n;
                    }

                    return Fib(n - 1) + Fib(n - 2);
                }
            }
            """,
            Warning(3, 5));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ComplexityCeilingMembershipScanCodeFix_FixesAll_AcrossTwoSites()
    {
        return VerifyMembershipAsync(
            TwoSiteSource,
            TwoSiteFixed,
            Warning(10, 13),
            Warning(26, 13));
    }
}

using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class LinearLookupInLoopBranchCoverageTests
{
    private static DiagnosticResult Expected(int markupIndex, string method, string receiver)
    {
        return new DiagnosticResult("E128066", DiagnosticSeverity.Warning)
            .WithLocation(markupIndex)
            .WithArguments(method, receiver);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fires_ForWhereFollowedByCount_AndNamesTheReceiverExpression()
    {
        return AnalyzerTestRunner.VerifyAsync<LinearLookupInLoopAnalyzer>(
            """
            using System.Collections.Generic;
            using System.Linq;
            class C
            {
                void M(List<int> items, List<int> lookup)
                {
                    foreach (var item in items)
                    {
                        var count = {|#0:lookup.Where(x => x == item).Count()|};
                    }
                }
            }
            """,
            Expected(0, "Count", "lookup.Where(x => x == item)"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForCountThatIsNotAWhereCountPattern()
    {
        return AnalyzerTestRunner.VerifyAsync<LinearLookupInLoopAnalyzer>(
            """
            using System.Collections.Generic;
            using System.Linq;
            class C
            {
                void M(List<int> items, List<int> lookup)
                {
                    foreach (var item in items)
                    {
                        var plain = lookup.Count();
                        var filtered = lookup.Count(x => x == item);
                        var selected = lookup.Select(x => x + item).Count();
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fires_AndNamesTheReceiverByItsMemberOrItsExpression()
    {
        return AnalyzerTestRunner.VerifyAsync<LinearLookupInLoopAnalyzer>(
            """
            using System.Collections.Generic;
            class C
            {
                private readonly List<int> _lookup = new();

                List<int> GetLookup() => _lookup;

                void M(List<int> items)
                {
                    foreach (var item in items)
                    {
                        _ = {|#0:this._lookup.Contains(item)|};
                        _ = {|#1:GetLookup().Contains(item)|};
                    }
                }
            }
            """,
            Expected(0, "Contains", "_lookup"),
            Expected(1, "Contains", "GetLookup()"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCallIsNotAMemberAccess()
    {
        return AnalyzerTestRunner.VerifyAsync<LinearLookupInLoopAnalyzer>(
            """
            using System.Collections.Generic;
            class C
            {
                bool Contains(int value) => value > 0;

                void M(List<int> items)
                {
                    foreach (var item in items)
                    {
                        _ = Contains(item);
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLambdaIsNotALinqArgument()
    {
        return AnalyzerTestRunner.VerifyAsync<LinearLookupInLoopAnalyzer>(
            """
            using System;
            using System.Collections.Generic;
            class C
            {
                static void Run(Func<int, bool> test)
                {
                }

                void M(List<int> lookup)
                {
                    Func<int, bool> assigned = y => lookup.Contains(y);
                    Run(y => lookup.Contains(y));
                    Action<Func<int, bool>> action = Run;
                    action(y => lookup.Contains(y));
                    new List<int>().Sort((a, b) => lookup.Contains(a) ? 0 : 1);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fires_WhenALambdaOnADelegateRunsInsideALoop()
    {
        return AnalyzerTestRunner.VerifyAsync<LinearLookupInLoopAnalyzer>(
            """
            using System;
            using System.Collections.Generic;
            class C
            {
                void M(List<int> items, List<int> lookup)
                {
                    foreach (var item in items)
                    {
                        Func<bool> check = () => {|E128066:lookup.Contains(item)|};
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConstantBoundForLoop_DependsOnWhichSideHoldsTheLiteral()
    {
        return AnalyzerTestRunner.VerifyAsync<LinearLookupInLoopAnalyzer>(
            """
            using System.Collections.Generic;
            class C
            {
                bool Check(int i) => i > 0;

                void M(List<int> lookup, int n)
                {
                    for (var i = 0; 10 > i; i++)
                    {
                        _ = lookup.Contains(i);
                    }

                    for (var i = 0; Check(i); i++)
                    {
                        _ = {|E128066:lookup.Contains(i)|};
                    }

                    for (var i = 0; i < n; i++)
                    {
                        _ = {|E128066:lookup.Contains(i)|};
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fires_WhenAForeachSitsInsideAConstantBoundLoop()
    {
        return AnalyzerTestRunner.VerifyAsync<LinearLookupInLoopAnalyzer>(
            """
            using System.Collections.Generic;
            class C
            {
                void M(List<int> items, List<int> lookup)
                {
                    for (var round = 0; round < 3; round++)
                    {
                        foreach (var item in items)
                        {
                            _ = {|E128066:lookup.Contains(item)|};
                        }
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheMethodIsAFieldHoldingADelegate()
    {
        return AnalyzerTestRunner.VerifyAsync<LinearLookupInLoopAnalyzer>(
            """
            using System;
            using System.Collections.Generic;
            class Holder
            {
                public Func<int, bool> Remove = _ => true;
            }

            class C
            {
                void M(List<int> items, Holder holder)
                {
                    foreach (var item in items)
                    {
                        _ = holder.Remove(item);
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fires_WhenOverloadResolutionFailsButCandidatesExist()
    {
        return AnalyzerTestRunner.VerifyAsync<LinearLookupInLoopAnalyzer>(
            """
            using System.Collections.Generic;
            class C
            {
                void M(List<int> items, List<int> lookup)
                {
                    foreach (var item in items)
                    {
                        _ = {|E128066:lookup.Contains(item, item, item)|};
                    }
                }
            }
            """,
            options: new AnalyzerTestOptions { CompilerDiagnostics = CompilerDiagnostics.None });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheQueryableIsAClass()
    {
        return AnalyzerTestRunner.VerifyAsync<LinearLookupInLoopAnalyzer>(
            """
            using System.Collections.Generic;
            using System.Linq;
            class C
            {
                void M(List<int> ids, EnumerableQuery<int> query)
                {
                    foreach (var id in ids)
                    {
                        var found = query.Any(x => x == id);
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForFrozenAndImmutableLookups()
    {
        return AnalyzerTestRunner.VerifyAsync<LinearLookupInLoopAnalyzer>(
            """
            using System.Collections.Frozen;
            using System.Collections.Generic;
            using System.Collections.Immutable;
            class C
            {
                void M(
                    List<int> ids,
                    FrozenSet<int> frozen,
                    ImmutableHashSet<int> immutable,
                    IReadOnlySet<int> readOnlySet)
                {
                    foreach (var id in ids)
                    {
                        _ = frozen.Contains(id) || immutable.Contains(id) || readOnlySet.Contains(id);
                    }
                }
            }
            """);
    }
}

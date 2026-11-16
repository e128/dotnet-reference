using System;
using System.Threading.Tasks;
using E128.Analyzers.Design;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ImplicitStateMachineAnalyzerBranchCoverageTests
{
    private const string FourFlagsBody = """
        {
            private bool _a;
            private bool _b;
            private bool _c;
            private bool _d;
            private bool _once;
            private int _count;

            public void SetA(bool value) { _a = value; }
            public void SetB(bool value) { _b = value; }
            public void SetC(bool value) { _c = value; }
            public void SetD(bool value) { _d = value; }
            public void Arm() { _once = true; }

        """;

    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<ImplicitStateMachineAnalyzer>(code);
    }

    private static Task VerifyReadAsync(string condition, bool fires, string extraMembers = "")
    {
        var name = fires ? "{|E128218:Machine|}" : "Machine";
        return VerifyAsync(
            "sealed class " + name + "\n" + FourFlagsBody + extraMembers + "    public void Run() { " + condition + " }\n}\n");
    }

    private static Task VerifyForwardingAsync(
        Func<string, string, string> getter,
        Func<string, string, string> setter,
        bool fires)
    {
        var name = fires ? "{|E128218:Machine|}" : "Machine";
        var code = "sealed class " + name + "\n{\n"
                   + "    private bool _a;\n    private bool _b;\n    private bool _c;\n"
                   + Property("A", getter, setter, "_a", "_b")
                   + Property("B", getter, setter, "_b", "_c")
                   + Property("C", getter, setter, "_c", "_a")
                   + "    public void X() { A = true; B = false; C = true; }\n"
                   + "    public void Y() { A = false; B = true; C = false; }\n}\n";
        return VerifyAsync(code);
    }

    private static string Property(
        string name,
        Func<string, string, string> getter,
        Func<string, string, string> setter,
        string field,
        string other)
    {
        return "    public bool " + name + " { " + getter(field, other) + " " + setter(field, other) + " }\n";
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForCompoundAssignmentsOfThreeFlags()
    {
        return VerifyAsync("""
            sealed class {|E128218:Machine|}
            {
                private bool _a;
                private bool _b;
                private bool _c;

                public void Merge(bool x)
                {
                    _a |= x;
                    _b &= x;
                    _c |= x;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForWritesThatTargetNoCandidateField()
    {
        return VerifyAsync("""
            sealed class Machine
            {
                private bool _a;
                private bool _b;
                private bool _c;
                private int _count;
                private readonly bool[] _items = new bool[2];

                public void Run(Machine other, bool x)
                {
                    var local = false;
                    local = true;
                    _count = 1;
                    _items[0] = true;
                    (_a, _b) = (true, false);
                    other._a = true;
                    other._b = false;
                    other._c = x;
                    _ = local;
                }
            }

            static class Registry
            {
                internal static bool Shared;
            }

            record Settings
            {
                public bool Flag { get; set; }
            }

            sealed class Writer
            {
                public void Run(Settings settings)
                {
                    Registry.Shared = true;
                    settings.Flag = true;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAnUnclusteredFlagIsOnlySetOneWay()
    {
        return VerifyAsync("""
            sealed class {|E128218:Machine|}
            {
                private bool _a;
                private bool _b;
                private bool _c;
                private bool _once;

                public void Start()
                {
                    _once = true;
                    _a = true;
                    _b = false;
                    _c = false;
                }

                public void Stop()
                {
                    _a = false;
                    _b = true;
                    _c = true;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_AtTheEarliestOfTwoCoWriteBodies()
    {
        return VerifyAsync("""
            sealed class {|E128218:Machine|}
            {
                private bool _a;
                private bool _b;
                private bool _c;

                public void First()
                {
                    _a = true;
                    _b = false;
                }

                public void Second()
                {
                    _a = false;
                    _b = true;
                    _c = true;
                }

                public void Third()
                {
                    _c = false;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForNotEqualsTrueNegation()
    {
        return VerifyReadAsync("if (_a && _b != true) { }", true);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForTrueNotEqualsFlagNegation()
    {
        return VerifyReadAsync("if (_a && true != _b) { }", true);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForFalseEqualsFlagNegation()
    {
        return VerifyReadAsync("if (_a && false == _b) { }", true);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForNotTruePatternNegation()
    {
        return VerifyReadAsync("if (_a && _b is not true) { }", true);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForTernaryCondition()
    {
        return VerifyReadAsync("var x = _a && !_b ? 1 : 2; _ = x;", true);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForForLoopCondition()
    {
        return VerifyReadAsync("for (var i = 0; _a && !_b; i++) { }", true);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenNonFlagMembersJoinTheCondition()
    {
        return VerifyReadAsync("if (!_a && _b && _count > 0) { }", true);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_AtTheEarliestOfTwoCoReads()
    {
        return VerifyReadAsync("if (_a && !_b) { } if (_c && !_d) { }", true);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForEqualsTrueAndNotEqualsFalseComparisons()
    {
        return VerifyReadAsync("if (_a && _b == true && _c != false) { }", false);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForNotFalsePatternAndPlainConstantPattern()
    {
        return VerifyReadAsync("if (_a && _b is not false && _c is true) { }", false);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheNegatedFlagIsOutsideTheCluster()
    {
        return VerifyReadAsync("if (_a && _b && !_once) { }", false);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenOnlyOneClusterFlagIsRead()
    {
        return VerifyReadAsync("if (_a && !_once) { }", false);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_AfterEmptyForAndForeachLoops()
    {
        return VerifyReadAsync(
            "for (; ; ) { break; } foreach (var item in new int[0]) { _ = item; } do { } while (_a && !_b);",
            true);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForConditionsOnAnotherInstance()
    {
        return VerifyReadAsync(
            string.Empty,
            false,
            "    public void Peek(Machine other) { if (other._a && !other._b) { } }\n");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForConditionsInTypesThatAreNotAnalyzed()
    {
        return VerifyAsync("""
            static class Gate
            {
                private static bool s_a;
                private static bool s_b;

                public static void Run(bool x)
                {
                    if (x && !s_a && s_b)
                    {
                    }

                    s_a = x;
                    s_b = !x;
                }
            }

            sealed class Plain
            {
                private int _n;

                public void Run(bool x)
                {
                    if (x && !x)
                    {
                        _n++;
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForInterlockedAndVolatileCallsThatDoNotWriteAFlag()
    {
        return VerifyAsync("""
            using System.Threading;

            sealed class Machine
            {
                private bool _a;
                private bool _b;
                private bool _c;
                private int _count;

                public void Run(Machine other)
                {
                    var local = false;
                    Interlocked.Exchange(ref local, true);
                    Interlocked.Exchange(ref other._a, true);
                    Interlocked.Exchange(ref _count, 1);
                    Interlocked.Increment(ref _count);
                    Volatile.Write(ref other._b, false);
                    Volatile.Read(ref _c);
                    Volatile.Write(ref _count, 3);
                }
            }

            static class Counter
            {
                private static int s_value;

                public static void Run()
                {
                    Interlocked.Exchange(ref s_value, 1);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForBlockBodiedForwardingAccessors()
    {
        return VerifyForwardingAsync(static (f, _) => $"get {{ return {f}; }}", static (f, _) => $"set {{ {f} = value; }}", true);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForForwardingAccessorsThatUseThis()
    {
        return VerifyForwardingAsync(static (f, _) => $"get => this.{f};", static (f, _) => $"set => this.{f} = value;", true);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForMixedBlockAndArrowForwardingAccessors()
    {
        return VerifyForwardingAsync(static (f, _) => $"get => {f};", static (f, _) => $"set {{ {f} = value; }}", true);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForBlockGetterWithArrowSetter()
    {
        return VerifyForwardingAsync(static (f, _) => $"get {{ return {f}; }}", static (f, _) => $"set => {f} = value;", true);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheGetterBodyHasTwoStatements()
    {
        return VerifyForwardingAsync(static (f, _) => $"get {{ var x = {f}; return x; }}", static (f, _) => $"set {{ {f} = value; }}", false);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheSetterBodyHasTwoStatements()
    {
        return VerifyForwardingAsync(static (f, _) => $"get => {f};", static (f, _) => $"set {{ {f} = value; _ = value; }}", false);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheSetterStoresAnotherValue()
    {
        return VerifyForwardingAsync(static (f, _) => $"get => {f};", static (f, _) => $"set {{ {f} = !value; }}", false);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheSetterIsACompoundAssignment()
    {
        return VerifyForwardingAsync(static (f, _) => $"get => {f};", static (f, _) => $"set => {f} |= value;", false);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheGetterReturnsAnExpression()
    {
        return VerifyForwardingAsync(static (f, _) => $"get => !{f};", static (f, _) => $"set => {f} = value;", false);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheGetterAndSetterNameDifferentFields()
    {
        return VerifyForwardingAsync(static (f, _) => $"get => {f};", static (_, o) => $"set => {o} = value;", false);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenThePropertyHasOnlyASetter()
    {
        return VerifyForwardingAsync(static (_, _) => string.Empty, static (f, _) => $"set => {f} = value;", false);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheGetterForwardsToAProperty()
    {
        return VerifyAsync("""
            sealed class Machine
            {
                private static bool s_shared;
                public bool A { get => B; set => B = value; }
                public bool B { get; set; }
                public bool C { get; set; }
                public bool S { get => s_shared; set => s_shared = value; }

                public void X() { A = true; S = true; }
                public void Y() { A = false; S = false; }
            }
            """);
    }
}

using System.Threading.Tasks;
using E128.Analyzers.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class LowValueTestAnalyzerBranchCoverageGapTests
{
    private static readonly ReferenceAssemblies Net100WithXunit = ReferenceAssemblies.Net.Net100
        .AddPackages([
            new PackageIdentity("xunit.v3.core", "3.2.2"),
            new PackageIdentity("xunit.v3.assert", "3.2.2")
        ]);

    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<LowValueTestAnalyzer>(
            code,
            options: new AnalyzerTestOptions { References = Net100WithXunit });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheTestCallsACheckMethodThatReturnsAValue()
    {
        return VerifyAsync("""
            using Xunit;

            public sealed class Rule
            {
                public bool Check(int value) => value > 0;
            }

            public sealed class Subject
            {
                [Fact]
                public void Should_AcceptThePositiveValue()
                {
                    var rule = new Rule();
                    rule.Check(1);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_BareSizeWhenTheMemberAccessesAreTypesFixturesOrUnresolved()
    {
        return VerifyAsync("""
            using Xunit;

            public sealed class Subject
            {
                private readonly int fixture = 2;

                [Fact]
                public void {|E128107:Should_ReadFixture|}()
                {
                    var type = typeof(System.String);
                    var right = this.fixture;
                    Assert.Equal(2, right);
                }

                [Fact]
                public void {|E128107:Should_ReadMissingMember|}()
                {
                    var left = 1;
                    var right = left.{|CS1061:Missing|};
                    Assert.NotEqual(0, 1);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheAssertedLiteralIsNotAReturnedProductionLiteral()
    {
        return VerifyAsync("""
            using System;
            using Xunit;

            public delegate int Transform(int value);

            public static class Calc
            {
                [Obsolete("legacy")]
                public static int Compute(int left, int right = 5)
                {
                    var offset = 9;
                    return left + right + offset;
                }

                public static Transform Make() => value => value + 7;
            }

            public sealed class Subject
            {
                [Fact]
                public void Should_ComputeTheTotal()
                {
                    var total = Calc.Compute(1);
                    Assert.Equal(5, total);
                    Assert.Equal(9, total);
                    Assert.Equal("legacy", total.ToString());
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheAssertedLiteralIsReturnedThroughADelegateTypedCall()
    {
        return VerifyAsync("""
            using Xunit;

            public delegate int Transform(int value);

            public static class Calc
            {
                public static Transform Make() => value => value + 7;
            }

            public sealed class Subject
            {
                [Fact]
                public void {|E128107:Should_RunTheTransform|}()
                {
                    var transform = Calc.Make();
                    var result = transform(1);
                    Assert.Equal(7, result);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_OrderLockForAnInterfaceTypedSetAndSilentForANullActualValue()
    {
        return VerifyAsync("""
            using System.Collections.Generic;
            using Xunit;

            public sealed class Subject
            {
                [Fact]
                public void Should_AcceptNull()
                {
                    var value = "a";
                    var other = 1;
                    Assert.Equal<string>(value, null);
                    Assert.Equal(1, other);
                }

                [Fact]
                public void {|E128107:Should_ReadTheSet|}()
                {
                    var expected = new[] { 1, 2 };
                    ISet<int> actual = new HashSet<int> { 1, 2 };
                    var other = 1;
                    Assert.Equal(expected, actual);
                    Assert.Equal(1, other);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ConstructorPassthroughOnlyForAMatchingLiteral()
    {
        return VerifyAsync("""
            using Xunit;

            public sealed class Box
            {
                public Box(int value, string name)
                {
                    Value = value;
                    Name = name;
                }

                public Box() { }

                public int Value { get; set; }

                public string Name { get; set; } = string.Empty;
            }

            public sealed class Subject
            {
                [Fact]
                public void {|E128107:Should_KeepTheValue|}()
                {
                    var name = "x";
                    Assert.Equal(5, new Box(5, name).Value);
                }

                [Fact]
                public void Should_KeepTheInitializedValue()
                {
                    var name = "x";
                    Assert.Equal(5, new Box { Value = 5 }.Value);
                }

                [Fact]
                public void Should_KeepAnotherValue()
                {
                    var name = "x";
                    var box = new Box(5, name);
                    Assert.Equal(7, box.Value);
                }

            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheConstructedLocalIsNotAnExplicitObjectCreation()
    {
        return VerifyAsync("""
            using Xunit;

            public sealed class Box
            {
                public Box(int value, string name)
                {
                    Value = value;
                }

                public int Value { get; set; }
            }

            public sealed class Subject
            {
                [Fact]
                public void Should_KeepTheTargetTypedValue()
                {
                    Box box = new(5, "x");
                    Assert.Equal(5, box.Value);
                }

                [Fact]
                public void Should_KeepTheFactoryValue()
                {
                    var box = Create();
                    Assert.Equal(5, box.Value);
                }

                [Fact]
                public void Should_KeepTheAssignedValue()
                {
                    Box box;
                    box = new Box(5, "x");
                    Assert.Equal(5, box.Value);
                }

                private static Box Create() => new Box(5, "x");
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheAssertedLiteralIsReturnedByAReturnStatement()
    {
        return VerifyAsync("""
            using Xunit;

            public static class Calc
            {
                public static int Answer()
                {
                    return 42;
                }
            }

            public sealed class Subject
            {
                [Fact]
                public void {|E128107:Should_ReturnTheAnswer|}()
                {
                    var left = Calc.Answer();
                    var right = 1;
                    Assert.Equal(42, left);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAnAssertionComparesANameofExpression()
    {
        return VerifyAsync("""
            using Xunit;

            public sealed class Foo { }

            public sealed class Subject
            {
                [Fact]
                public void Should_NameTheType()
                {
                    var left = 1;
                    var right = 2;
                    Assert.Equal("Foo", nameof(Foo));
                    Assert.NotEqual(left, right);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAnAssertionArgumentHoldsALambdaInsideAnObjectCreation()
    {
        return VerifyAsync("""
            using System;
            using Xunit;

            public sealed class Box
            {
                public Box(Func<int> read)
                {
                    Value = read();
                }

                public int Value { get; }
            }

            public sealed class Subject
            {
                [Fact]
                public void Should_ReadTheLambda()
                {
                    var left = 1;
                    var right = 2;
                    Assert.Equal(1, new Box(() => left + right).Value);
                    Assert.NotEqual(left, right);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_LogAssertOnlyForAReceiverThatNamesALogger()
    {
        return VerifyAsync("""
            using Xunit;

            public sealed class Recorder
            {
                public void VerifyLogged(string message) { }
            }

            public sealed class Fixture
            {
                public Recorder Logger { get; } = new Recorder();

                public Recorder Make() => new Recorder();
            }

            public sealed class Subject
            {
                [Fact]
                public void {|E128107:Should_LogTheMessage|}()
                {
                    var fixture = new Fixture();
                    fixture.Logger.VerifyLogged("x");
                }

                [Fact]
                public void Should_NotLogThroughACall()
                {
                    var fixture = new Fixture();
                    fixture.Make().VerifyLogged("x");
                }

                [Fact]
                public void Should_NotLogThroughAnUnqualifiedCall()
                {
                    var fixture = new Fixture();
                    VerifyLogged("x");
                }

                private static void VerifyLogged(string message) { }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_OnlyConstructorPassthroughForAnImportedEqualOnAnExceptionMessage()
    {
        return VerifyAsync("""
            using System;
            using static Xunit.Assert;
            using Xunit;

            public sealed class Subject
            {
                [Fact]
                public void {|E128107:Should_CarryTheMessage|}()
                {
                    var exception = new InvalidOperationException("boom");
                    Equal("boom", exception.Message);
                }
            }
            """);
    }
}

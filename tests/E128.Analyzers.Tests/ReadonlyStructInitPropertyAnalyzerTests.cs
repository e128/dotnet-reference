using System.Threading.Tasks;
using E128.Analyzers.Design;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ReadonlyStructInitPropertyAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_GetOnlyProperty_InReadonlyStruct()
    {
        return AnalyzerTestRunner.VerifyAsync<ReadonlyStructInitPropertyAnalyzer>("""
                           public readonly struct Point
                           {
                               public int {|E128074:X|} { get; }
                               public int {|E128074:Y|} { get; }
                               public Point(int x, int y) { X = x; Y = y; }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_WhenPropertyHasInit()
    {
        return AnalyzerTestRunner.VerifyAsync<ReadonlyStructInitPropertyAnalyzer>("""
                           public readonly struct Point
                           {
                               public int X { get; init; }
                               public int Y { get; init; }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_WhenNonReadonlyStruct()
    {
        return AnalyzerTestRunner.VerifyAsync<ReadonlyStructInitPropertyAnalyzer>("""
                           public struct MutablePoint
                           {
                               public int X { get; }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_WhenClass()
    {
        return AnalyzerTestRunner.VerifyAsync<ReadonlyStructInitPropertyAnalyzer>("""
                           public class MyClass
                           {
                               public int X { get; }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_WhenStaticProperty()
    {
        return AnalyzerTestRunner.VerifyAsync<ReadonlyStructInitPropertyAnalyzer>("""
                           public readonly struct Config
                           {
                               public static int DefaultValue { get; } = 42;
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_WhenExpressionBodiedComputedProperty()
    {
        return AnalyzerTestRunner.VerifyAsync<ReadonlyStructInitPropertyAnalyzer>("""
                           public readonly struct Wrapper
                           {
                               public int Value { get; init; }
                               public int Doubled => Value * 2;
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_WhenGetAccessorHasABody()
    {
        return AnalyzerTestRunner.VerifyAsync<ReadonlyStructInitPropertyAnalyzer>("""
                           public readonly struct Wrapper
                           {
                               private readonly int _value;
                               public int Value { get { return _value; } }
                               public Wrapper(int value) => _value = value;
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_WhenGetAccessorHasAnExpressionBody()
    {
        return AnalyzerTestRunner.VerifyAsync<ReadonlyStructInitPropertyAnalyzer>("""
                           public readonly struct Wrapper
                           {
                               private readonly int _value;
                               public int Value { get => _value; }
                               public Wrapper(int value) => _value = value;
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_OnlyGetOnlyProperties_MixedWithInit()
    {
        return AnalyzerTestRunner.VerifyAsync<ReadonlyStructInitPropertyAnalyzer>("""
                           public readonly struct Mixed
                           {
                               public int {|E128074:A|} { get; }
                               public int B { get; init; }
                               public int {|E128074:C|} { get; }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_WhenReadonlyRecord()
    {
        return AnalyzerTestRunner.VerifyAsync<ReadonlyStructInitPropertyAnalyzer>("""
                           public readonly record struct PointRecord(int X, int Y);
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_WhenPropertyIsIndexer()
    {
        return AnalyzerTestRunner.VerifyAsync<ReadonlyStructInitPropertyAnalyzer>("""
                           public readonly struct Wrapper
                           {
                               private readonly int[] _data;
                               public Wrapper(int[] data) { _data = data; }
                               public int this[int i] => _data[i];
                           }
                           """);
    }
}

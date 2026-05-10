using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ReadonlyStructInitPropertyAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<ReadonlyStructInitPropertyAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_GetOnlyProperty_InReadonlyStruct()
    {
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
                           public class MyClass
                           {
                               public int X { get; }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_WhenPropertyHasSet()
    {
        return VerifyAsync("""
                           public readonly struct Point
                           {
                               public int X { get; set; }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_WhenStaticProperty()
    {
        return VerifyAsync("""
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
        return VerifyAsync("""
                           public readonly struct Wrapper
                           {
                               public int Value { get; init; }
                               public int Doubled => Value * 2;
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_OnlyGetOnlyProperties_MixedWithInit()
    {
        return VerifyAsync("""
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
        return VerifyAsync("""
                           public readonly record struct PointRecord(int X, int Y);
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_WhenPropertyIsIndexer()
    {
        return VerifyAsync("""
                           public readonly struct Wrapper
                           {
                               private readonly int[] _data;
                               public Wrapper(int[] data) { _data = data; }
                               public int this[int i] => _data[i];
                           }
                           """);
    }
}

using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ReadonlyMutableStructFieldAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAReadonlyFieldOfAMutableStruct()
    {
        return AnalyzerTestRunner.VerifyAsync<ReadonlyMutableStructFieldAnalyzer>("""
            struct Counter
            {
                public int Value;

                public void Increment()
                {
                    Value++;
                }
            }

            class C
            {
                private readonly Counter {|E128247:_counter|} = default;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAStaticReadonlyField()
    {
        return AnalyzerTestRunner.VerifyAsync<ReadonlyMutableStructFieldAnalyzer>("""
            struct Counter
            {
                public void Increment()
                {
                }
            }

            class C
            {
                private static readonly Counter {|E128247:_counter|} = default;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAReadonlyStructType()
    {
        return AnalyzerTestRunner.VerifyAsync<ReadonlyMutableStructFieldAnalyzer>("""
            readonly struct Frozen
            {
                public int GetValue() => 1;
            }

            class C
            {
                private readonly Frozen _frozen = default;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAMutableField()
    {
        return AnalyzerTestRunner.VerifyAsync<ReadonlyMutableStructFieldAnalyzer>("""
            struct Counter
            {
                public void Increment()
                {
                }
            }

            class C
            {
                private Counter _counter = default;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAPrimitiveField()
    {
        return AnalyzerTestRunner.VerifyAsync<ReadonlyMutableStructFieldAnalyzer>("""
            class C
            {
                private readonly int _count = 0;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForATupleField()
    {
        return AnalyzerTestRunner.VerifyAsync<ReadonlyMutableStructFieldAnalyzer>("""
            class C
            {
                private readonly (int A, int B) _pair = default;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheStructHasOnlyReadonlyMembers()
    {
        return AnalyzerTestRunner.VerifyAsync<ReadonlyMutableStructFieldAnalyzer>("""
            struct Frozen
            {
                public readonly int GetValue() => 1;
            }

            class C
            {
                private readonly Frozen _frozen = default;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAClassTypedField()
    {
        return AnalyzerTestRunner.VerifyAsync<ReadonlyMutableStructFieldAnalyzer>("""
            class C
            {
                private readonly object _value = new();
            }
            """);
    }
}

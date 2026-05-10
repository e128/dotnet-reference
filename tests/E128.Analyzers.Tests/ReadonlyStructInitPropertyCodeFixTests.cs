using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ReadonlyStructInitPropertyCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<ReadonlyStructInitPropertyAnalyzer, ReadonlyStructInitPropertyCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_AddsInit_ToGetOnlyProperty()
    {
        return VerifyFixAsync(
            """
            public readonly struct Point
            {
                public int {|E128074:X|} { get; }
                public Point(int x) { X = x; }
            }
            """,
            """
            public readonly struct Point
            {
                public int X { get; init; }
                public Point(int x) { X = x; }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_PreservesTrivia()
    {
        return VerifyFixAsync(
            """
            public readonly struct Config
            {
                /// <summary>The name.</summary>
                public string {|E128074:Name|} { get; }
            }
            """,
            """
            public readonly struct Config
            {
                /// <summary>The name.</summary>
                public string Name { get; init; }
            }
            """);
    }
}

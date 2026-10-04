using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ByteSizeForDataSizeE128CodeFixTests
{
    // The rule and the fix both target Pug.Core.Classes.ByteSize. A stub keeps the test off the
    // package and off the product assembly.
    private const string ByteSizeStub = """
                                        namespace Pug.Core.Classes
                                        {
                                            public readonly struct ByteSize
                                            {
                                                public static ByteSize FromBytes(long bytes) => default;
                                            }
                                        }
                                        """;

    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        var test = new CSharpCodeFixTest<ByteSizeForDataSizeAnalyzer, ByteSizeForDataSizeCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.TestState.Sources.Add(ByteSizeStub);
        test.FixedState.Sources.Add(ByteSizeStub);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PropertyIntToByteSize_Fixed()
    {
        return VerifyFixAsync(
            """
            class C
            {
                public int {|E128080:MaxSizeBytes|} { get; set; }
            }
            """,
            """
            using Pug.Core.Classes;
            class C
            {
                public ByteSize MaxSizeBytes { get; set; }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ParameterWithDefaultValue_OffersNoFix()
    {
        // A default value must stay a compile-time constant, and ByteSize.FromBytes is a method call,
        // so the fixed state keeps the diagnostic instead of gaining a rewritten parameter.
        return VerifyFixAsync(
            """
            class C
            {
                public void M(long {|E128080:MaxSizeBytes|} = 0) { }
            }
            """,
            """
            class C
            {
                public void M(long {|E128080:MaxSizeBytes|} = 0) { }
            }
            """);
    }
}

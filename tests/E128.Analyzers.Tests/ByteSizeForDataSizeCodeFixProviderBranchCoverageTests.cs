using System.Threading.Tasks;
using E128.Analyzers.Design;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ByteSizeForDataSizeCodeFixProviderBranchCoverageTests
{
    // The rule and the fix both target Pug.Core.Classes.ByteSize. A stub keeps the test off the package.
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
        return CodeFixTestRunner.RunAsync<ByteSizeForDataSizeAnalyzer, ByteSizeForDataSizeCodeFixProvider>(
            source,
            fixedCode,
            options: new CodeFixTestOptions { Sources = [ByteSizeStub] });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_WrapsThePropertyInitializer()
    {
        return VerifyFixAsync(
            """
            class C
            {
                public int {|E128080:MaxSizeBytes|} { get; set; } = 1024;
            }
            """,
            """
            using Pug.Core.Classes;

            class C
            {
                public ByteSize MaxSizeBytes { get; set; } = Pug.Core.Classes.ByteSize.FromBytes(1024);
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_ChangesTheTypeOfAParameterWithoutADefault()
    {
        return VerifyFixAsync(
            """
            class C
            {
                void M(int {|E128080:bufferBytes|})
                {
                }
            }
            """,
            """
            using Pug.Core.Classes;

            class C
            {
                void M(ByteSize bufferBytes)
                {
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_WrapsTheInitializerOfAField()
    {
        return VerifyFixAsync(
            """
            class C
            {
                private int {|E128080:_maxBufferSize|} = 4096, {|E128080:_minBufferSize|};
            }
            """,
            """
            using Pug.Core.Classes;

            class C
            {
                private ByteSize _maxBufferSize = Pug.Core.Classes.ByteSize.FromBytes(4096), _minBufferSize;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_TurnsAConstFieldIntoAStaticReadonlyField()
    {
        return VerifyFixAsync(
            """
            class C
            {
                private const int {|E128080:MaxBufferSize|} = 4096;
            }
            """,
            """
            using Pug.Core.Classes;

            class C
            {
                private static readonly ByteSize MaxBufferSize = Pug.Core.Classes.ByteSize.FromBytes(4096);
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_DropsConstFromALocal()
    {
        return VerifyFixAsync(
            """
            class C
            {
                void M()
                {
                    const int {|E128080:bufferBytes|} = 4096;
                }
            }
            """,
            """
            using Pug.Core.Classes;

            class C
            {
                void M()
                {
                    ByteSize bufferBytes = Pug.Core.Classes.ByteSize.FromBytes(4096);
                }
            }
            """);
    }
}

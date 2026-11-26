using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class StackAllocSafetyCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return CodeFixTestRunner.RunAsync<StackAllocSafetyAnalyzer, StackAllocSafetyCodeFixProvider>(
            source,
            fixedCode,
            options: new CodeFixTestOptions { AllowUnsafe = true });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_AssignsTheBufferToASpan()
    {
        return VerifyFixAsync(
            """
            class C
            {
                unsafe void M()
                {
                    int* buffer = {|E128109:stackalloc int[16]|};
                    buffer[0] = 1;
                }
            }
            """,
            """
            using System;

            class C
            {
                unsafe void M()
                {
                    Span<int> buffer = stackalloc int[16];
                    buffer[0] = 1;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheStackAllocIsInsideALoop()
    {
        const string Code = """
            unsafe class C
            {
                void M()
                {
                    for (var i = 0; i < 3; i++)
                    {
                        int* buffer = {|E128109:stackalloc int[4]|};
                        buffer[0] = i;
                    }
                }
            }
            """;

        return VerifyFixAsync(Code, Code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheAddressOfAnElementIsTaken()
    {
        const string Code = """
            unsafe class C
            {
                void M()
                {
                    int* buffer = {|E128109:stackalloc int[4]|};
                    int* first = &buffer[0];
                }
            }
            """;

        return VerifyFixAsync(Code, Code);
    }
}

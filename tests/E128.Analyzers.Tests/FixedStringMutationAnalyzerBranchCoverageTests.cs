using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FixedStringMutationAnalyzerBranchCoverageTests
{
    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<FixedStringMutationAnalyzer>(code, options: new AnalyzerTestOptions { AllowUnsafe = true });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAnAssignmentThroughAPointerIndirection()
    {
        return VerifyAsync("""
            unsafe class C
            {
                void M(string text)
                {
                    {|E128126:fixed (char* pointer = text)
                    {
                        *pointer = 'a';
                    }|}
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAWriteThroughAnOffsetPointer()
    {
        return VerifyAsync("""
            unsafe class C
            {
                void M(string text)
                {
                    {|E128126:fixed (char* pointer = text)
                    {
                        *(pointer + 1) = 'a';
                    }|}
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAnIncrementOfTheCharacter()
    {
        return VerifyAsync("""
            unsafe class C
            {
                void M(string text)
                {
                    {|E128126:fixed (char* pointer = text)
                    {
                        (*pointer)++;
                    }|}
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAPreIncrementAndAPreDecrement()
    {
        return VerifyAsync("""
            unsafe class C
            {
                void M(string text, string other)
                {
                    {|E128126:fixed (char* pointer = text)
                    {
                        ++pointer[0];
                    }|}

                    {|E128126:fixed (char* pointer = other)
                    {
                        --pointer[0];
                    }|}
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAPostDecrement()
    {
        return VerifyAsync("""
            unsafe class C
            {
                void M(string text)
                {
                    {|E128126:fixed (char* pointer = text)
                    {
                        pointer[0]--;
                    }|}
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForARefOrOutArgument()
    {
        return VerifyAsync("""
            unsafe class C
            {
                static void Touch(ref char value) { }

                static void Fill(out char value) { value = 'a'; }

                void M(string text, string other)
                {
                    {|E128126:fixed (char* pointer = text)
                    {
                        Touch(ref pointer[0]);
                    }|}

                    {|E128126:fixed (char* pointer = other)
                    {
                        Fill(out pointer[0]);
                    }|}
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenALaterVariableOfTheFixedStatementIsWritten()
    {
        return VerifyAsync("""
            unsafe class C
            {
                void M(string text, string other)
                {
                    {|E128126:fixed (char* first = text, second = other)
                    {
                        second[0] = 'a';
                    }|}
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForReadsAndForOtherPointerTypes()
    {
        return VerifyAsync("""
            unsafe class C
            {
                static void Read(char value) { }

                static void Pass(in char value) { }

                void M(string text, int[] numbers, char[] buffer)
                {
                    fixed (char* pointer = text)
                    {
                        Read(*pointer);
                        Read(*(pointer + 1));
                        Read((pointer)[0]);
                        Pass(in pointer[0]);
                        var copy = pointer[0] + 1;
                        var other = pointer;
                        _ = copy;
                        _ = other;
                    }

                    fixed (int* count = numbers)
                    {
                        count[0] = 1;
                    }

                    fixed (char* local = buffer)
                    {
                        local[0] = 'a';
                    }
                }
            }
            """);
    }
}

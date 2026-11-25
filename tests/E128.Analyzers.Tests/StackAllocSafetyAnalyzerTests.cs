using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class StackAllocSafetyAnalyzerTests
{
    private static Task VerifyAsync(string code, bool allowUnsafe = false)
    {
        return AnalyzerTestRunner.VerifyAsync<StackAllocSafetyAnalyzer>(code, options: new AnalyzerTestOptions { AllowUnsafe = allowUnsafe });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenInsideLoop()
    {
        return VerifyAsync("""
                           using System;
                           class C
                           {
                               void M(int count)
                               {
                                   for (var i = 0; i < count; i++)
                                   {
                                       Span<byte> buffer = {|E128109:stackalloc byte[16]|};
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenConstantSizeExceedsLimit()
    {
        return VerifyAsync("""
                           using System;
                           class C
                           {
                               void M()
                               {
                                   Span<int> buffer = {|E128109:stackalloc int[300]|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenLengthIsUnbounded()
    {
        return VerifyAsync("""
                           using System;
                           class C
                           {
                               void M(int length)
                               {
                                   Span<byte> buffer = {|E128109:stackalloc byte[length]|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenSmallConstantSize()
    {
        return VerifyAsync("""
                           using System;
                           class C
                           {
                               void M()
                               {
                                   Span<byte> buffer = stackalloc byte[256];
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenGuardedByConditional()
    {
        return VerifyAsync("""
                           using System;
                           class C
                           {
                               void M(int length)
                               {
                                   Span<byte> buffer = length <= 256 ? stackalloc byte[length] : new byte[length];
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenLengthUsesMathMin()
    {
        return VerifyAsync("""
                           using System;
                           class C
                           {
                               void M(int length)
                               {
                                   Span<byte> buffer = stackalloc byte[Math.Min(length, 256)];
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenLoopIsOutsideLocalFunction()
    {
        return VerifyAsync("""
                           using System;
                           class C
                           {
                               void M(int count)
                               {
                                   for (var i = 0; i < count; i++)
                                   {
                                       Use(i);
                                   }

                                   void Use(int n)
                                   {
                                       Span<byte> buffer = stackalloc byte[16];
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenGuardIgnoresLength()
    {
        return VerifyAsync("""
                           using System;
                           class C
                           {
                               void M(int length, bool flag)
                               {
                                   if (flag)
                                   {
                                       Span<byte> buffer = {|E128109:stackalloc byte[length]|};
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheGuardChecksPositivityOnly()
    {
        return VerifyAsync("""
                           using System;
                           class C
                           {
                               void M(int length)
                               {
                                   if (length > 0)
                                   {
                                       Span<byte> buffer = {|E128109:stackalloc byte[length]|};
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheGuardAllowsMoreThanTheCap()
    {
        return VerifyAsync("""
                           using System;
                           class C
                           {
                               void M(int length)
                               {
                                   if (length <= 10_000)
                                   {
                                       Span<byte> buffer = {|E128109:stackalloc byte[length]|};
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenMathMinAllowsMoreThanTheCap()
    {
        return VerifyAsync("""
                           using System;
                           class C
                           {
                               void M(int length)
                               {
                                   Span<byte> buffer = {|E128109:stackalloc byte[Math.Min(length, 100_000)]|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAStructElementExceedsTheCap()
    {
        return VerifyAsync("""
                           using System;
                           struct Wide
                           {
                               public long A, B, C, D, E, F, G, H;
                           }
                           class C
                           {
                               void M()
                               {
                                   Span<Wide> buffer = {|E128109:stackalloc Wide[100]|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenASmallStructElementFits()
    {
        return VerifyAsync("""
                           using System;
                           struct Narrow
                           {
                               public long A;
                           }
                           class C
                           {
                               void M()
                               {
                                   Span<Narrow> buffer = stackalloc Narrow[100];
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAnEnumElementExceedsTheCap()
    {
        return VerifyAsync("""
                           using System;
                           enum Wide : long
                           {
                               A
                           }
                           class C
                           {
                               void M()
                               {
                                   Span<Wide> buffer = {|E128109:stackalloc Wide[300]|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenANativeIntElementExceedsTheCap()
    {
        return VerifyAsync("""
                           using System;
                           class C
                           {
                               void M()
                               {
                                   Span<nint> buffer = {|E128109:stackalloc nint[300]|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAssignedToPointer()
    {
        return VerifyAsync(
            """
            class C
            {
                unsafe void M()
                {
                    byte* buffer = {|E128109:stackalloc byte[16]|};
                }
            }
            """,
            allowUnsafe: true);
    }
}

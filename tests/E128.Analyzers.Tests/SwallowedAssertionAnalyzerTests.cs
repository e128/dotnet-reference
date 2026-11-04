using System.Threading.Tasks;
using E128.Analyzers.Testing;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SwallowedAssertionAnalyzerTests
{
    private static readonly ReferenceAssemblies Net100WithXunit = ReferenceAssemblies.Net.Net100
        .AddPackages([
            new PackageIdentity("xunit.v3.core", "3.2.2"),
            new PackageIdentity("xunit.v3.assert", "3.2.2")
        ]);

    private static Task VerifyAsync(string code)
    {
        return new CSharpAnalyzerTest<SwallowedAssertionAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = Net100WithXunit
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CatchExceptionWithoutRethrow_Fires()
    {
        return VerifyAsync("""
                           using System;
                           using Xunit;
                           public class T
                           {
                               [Fact]
                               public void A()
                               {
                                   try
                                   {
                                       Assert.Equal(1, 2);
                                   }
                                   {|E128172:catch|} (Exception)
                                   {
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task BareCatch_Fires()
    {
        return VerifyAsync("""
                           using Xunit;
                           public class T
                           {
                               [Fact]
                               public void A()
                               {
                                   try
                                   {
                                       Assert.True(false);
                                   }
                                   {|E128172:catch|}
                                   {
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CatchThatRethrows_Silent()
    {
        return VerifyAsync("""
                           using System;
                           using Xunit;
                           public class T
                           {
                               [Fact]
                               public void A()
                               {
                                   try
                                   {
                                       Assert.True(true);
                                   }
                                   catch (Exception)
                                   {
                                       throw;
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NarrowCatch_Silent()
    {
        return VerifyAsync("""
                           using System;
                           using Xunit;
                           public class T
                           {
                               [Fact]
                               public void A()
                               {
                                   try
                                   {
                                       Assert.True(true);
                                   }
                                   catch (InvalidOperationException)
                                   {
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CatchWithExceptionFilter_Silent()
    {
        return VerifyAsync("""
                           using System;
                           using Xunit;
                           public class T
                           {
                               [Fact]
                               public void A()
                               {
                                   try
                                   {
                                       Assert.True(true);
                                   }
                                   catch (Exception e) when (e is InvalidOperationException)
                                   {
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ThrowInsideNestedLambda_Fires()
    {
        return VerifyAsync("""
                           using System;
                           using Xunit;
                           public class T
                           {
                               [Fact]
                               public void A()
                               {
                                   try
                                   {
                                       Assert.True(true);
                                   }
                                   {|E128172:catch|} (Exception)
                                   {
                                       Action later = () => throw new InvalidOperationException();
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ThrowInsideLocalFunction_Fires()
    {
        return VerifyAsync("""
                           using System;
                           using Xunit;
                           public class T
                           {
                               [Fact]
                               public void A()
                               {
                                   try
                                   {
                                       Assert.True(true);
                                   }
                                   {|E128172:catch|} (Exception)
                                   {
                                       void Later() { throw new InvalidOperationException(); }
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CatchThatCallsAssertFail_Silent()
    {
        return VerifyAsync("""
                           using System;
                           using Xunit;
                           public class T
                           {
                               [Fact]
                               public void A()
                               {
                                   try
                                   {
                                       Assert.True(true);
                                   }
                                   catch (Exception e)
                                   {
                                       Assert.Fail(e.Message);
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TryWithoutAssert_Silent()
    {
        return VerifyAsync("""
                           using System;
                           public class T
                           {
                               public void A()
                               {
                                   try
                                   {
                                       Console.WriteLine("x");
                                   }
                                   catch (Exception)
                                   {
                                   }
                               }
                           }
                           """);
    }
}

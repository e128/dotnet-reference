using System.Threading.Tasks;
using E128.Analyzers.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SwallowedAssertionAnalyzerTests
{

    private static AnalyzerTestOptions Options => new() { References = Net100WithXunit };

    private static readonly ReferenceAssemblies Net100WithXunit = ReferenceAssemblies.Net.Net100
        .AddPackages([
            new PackageIdentity("xunit.v3.core", "3.2.2"),
            new PackageIdentity("xunit.v3.assert", "3.2.2")
        ]);

    [Fact]
    [Trait("Category", "CI")]
    public Task CatchExceptionWithoutRethrow_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<SwallowedAssertionAnalyzer>("""
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
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task BareCatch_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<SwallowedAssertionAnalyzer>("""
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
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CatchThatRethrows_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<SwallowedAssertionAnalyzer>("""
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
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NarrowCatch_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<SwallowedAssertionAnalyzer>("""
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
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CatchWithExceptionFilter_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<SwallowedAssertionAnalyzer>("""
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
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ThrowInsideNestedLambda_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<SwallowedAssertionAnalyzer>("""
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
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ThrowInsideLocalFunction_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<SwallowedAssertionAnalyzer>("""
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
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CatchThatCallsAssertFail_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<SwallowedAssertionAnalyzer>("""
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
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TryWithoutAssert_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<SwallowedAssertionAnalyzer>("""
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
                           """, options: Options);
    }
}

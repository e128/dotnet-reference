using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class CatchFilterMessageAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_MessageMember()
    {
        return AnalyzerTestRunner.VerifyAsync<CatchFilterMessageAnalyzer>("""
                           using System;
                           class C
                           {
                               void M()
                               {
                                   try
                                   {
                                   }
                                   catch (Exception ex) when ({|E128188:ex.Message|}.Contains("disk"))
                                   {
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenALambdaParameterShadowsTheName()
    {
        return AnalyzerTestRunner.VerifyAsync<CatchFilterMessageAnalyzer>("""
                           using System;
                           using System.Linq;
                           class Item { public string Message => ""; }
                           class C
                           {
                               void M(Item[] items)
                               {
                                   try
                                   {
                                   }
                                   catch (Exception ex) when (items.Any(ex => ex.Message.Length > 0))
                                   {
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_MessageThroughAConditionalAccess()
    {
        return AnalyzerTestRunner.VerifyAsync<CatchFilterMessageAnalyzer>("""
                           using System;
                           class C
                           {
                               void M()
                               {
                                   try
                                   {
                                   }
                                   catch (Exception ex) when ({|E128188:ex?.Message|} is not null)
                                   {
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_StackTraceMember()
    {
        return AnalyzerTestRunner.VerifyAsync<CatchFilterMessageAnalyzer>("""
                           using System;
                           class C
                           {
                               void M()
                               {
                                   try
                                   {
                                   }
                                   catch (Exception ex) when ({|E128188:ex.StackTrace|} is not null)
                                   {
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ToStringMember()
    {
        return AnalyzerTestRunner.VerifyAsync<CatchFilterMessageAnalyzer>("""
                           using System;
                           class C
                           {
                               void M()
                               {
                                   try
                                   {
                                   }
                                   catch (Exception ex) when ({|E128188:ex.ToString|}() != "x")
                                   {
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_MessageOnRenamedVariable()
    {
        return AnalyzerTestRunner.VerifyAsync<CatchFilterMessageAnalyzer>("""
                           using System;
                           class C
                           {
                               void M()
                               {
                                   try
                                   {
                                   }
                                   catch (InvalidOperationException failure) when ({|E128188:failure.Message|}.Length > 0)
                                   {
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_MessageAndStackTrace()
    {
        return AnalyzerTestRunner.VerifyAsync<CatchFilterMessageAnalyzer>("""
                           using System;
                           class C
                           {
                               void M()
                               {
                                   try
                                   {
                                   }
                                   catch (Exception ex) when ({|E128188:ex.Message|} is not null && {|E128188:ex.StackTrace|} is not null)
                                   {
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_HResultRead()
    {
        return AnalyzerTestRunner.VerifyAsync<CatchFilterMessageAnalyzer>("""
                           using System;
                           class C
                           {
                               void M()
                               {
                                   try
                                   {
                                   }
                                   catch (Exception ex) when (ex.HResult == -1)
                                   {
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_InnerExceptionRead()
    {
        return AnalyzerTestRunner.VerifyAsync<CatchFilterMessageAnalyzer>("""
                           using System;
                           class C
                           {
                               void M()
                               {
                                   try
                                   {
                                   }
                                   catch (Exception ex) when (ex.InnerException is not null)
                                   {
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_MessageOnOtherObject()
    {
        return AnalyzerTestRunner.VerifyAsync<CatchFilterMessageAnalyzer>("""
                           using System;
                           class Holder
                           {
                               public string Message { get; set; }
                           }
                           class C
                           {
                               void M(Holder other)
                               {
                                   try
                                   {
                                   }
                                   catch (Exception ex) when (other.Message is not null)
                                   {
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_MethodCallOnException()
    {
        return AnalyzerTestRunner.VerifyAsync<CatchFilterMessageAnalyzer>("""
                           using System;
                           using System.IO;
                           class C
                           {
                               void M()
                               {
                                   try
                                   {
                                   }
                                   catch (Exception ex) when (ex.GetType() == typeof(IOException))
                                   {
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_CatchDeclaresNoVariable()
    {
        return AnalyzerTestRunner.VerifyAsync<CatchFilterMessageAnalyzer>("""
                           using System;
                           class C
                           {
                               void M()
                               {
                                   try
                                   {
                                   }
                                   catch (Exception) when (Environment.TickCount > 0)
                                   {
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_MessageReadInCatchBody()
    {
        return AnalyzerTestRunner.VerifyAsync<CatchFilterMessageAnalyzer>("""
                           using System;
                           class C
                           {
                               void M()
                               {
                                   try
                                   {
                                   }
                                   catch (Exception ex)
                                   {
                                       string text = ex.Message;
                                   }
                               }
                           }
                           """);
    }
}

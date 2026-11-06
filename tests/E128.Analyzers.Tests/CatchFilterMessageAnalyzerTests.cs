using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class CatchFilterMessageAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<CatchFilterMessageAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_MessageMember()
    {
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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

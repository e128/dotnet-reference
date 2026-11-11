using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UseAfterDisposeAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task MemberCallAfterDispose_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<UseAfterDisposeAnalyzer>("""
                           using System.IO;
                           class C
                           {
                               void M()
                               {
                                   var stream = new MemoryStream();
                                   stream.Dispose();
                                   {|E128162:stream|}.WriteByte(1);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SecondDispose_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<UseAfterDisposeAnalyzer>("""
                           using System.IO;
                           class C
                           {
                               void M()
                               {
                                   var stream = new MemoryStream();
                                   stream.Dispose();
                                   stream.Dispose();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ReassignmentAfterDispose_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<UseAfterDisposeAnalyzer>("""
                           using System.IO;
                           class C
                           {
                               void M()
                               {
                                   var stream = new MemoryStream();
                                   stream.Dispose();
                                   stream = new MemoryStream();
                                   stream.WriteByte(1);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task UsingStatement_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<UseAfterDisposeAnalyzer>("""
                           using System.IO;
                           class C
                           {
                               void M()
                               {
                                   using var stream = new MemoryStream();
                                   stream.WriteByte(1);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LocalFunctionCalledBeforeDispose_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<UseAfterDisposeAnalyzer>("""
                           using System.IO;
                           class C
                           {
                               void M()
                               {
                                   var stream = new MemoryStream();
                                   Use();
                                   stream.Dispose();

                                   void Use() => stream.WriteByte(1);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LambdaBody_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<UseAfterDisposeAnalyzer>("""
                           using System;
                           using System.IO;
                           class C
                           {
                               void M()
                               {
                                   var stream = new MemoryStream();
                                   Action act = () => stream.WriteByte(1);
                                   stream.Dispose();
                                   act();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task UseBeforeDispose_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<UseAfterDisposeAnalyzer>("""
                           using System.IO;
                           class C
                           {
                               void M()
                               {
                                   var stream = new MemoryStream();
                                   stream.WriteByte(1);
                                   stream.Dispose();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task BracelessIfDispose_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<UseAfterDisposeAnalyzer>("""
                           using System.IO;
                           class C
                           {
                               void M(bool c)
                               {
                                   var stream = new MemoryStream();
                                   if (c) stream.Dispose();
                                   stream.WriteByte(1);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NameofAfterDispose_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<UseAfterDisposeAnalyzer>("""
                           using System.IO;
                           class C
                           {
                               string M()
                               {
                                   var stream = new MemoryStream();
                                   stream.Dispose();
                                   return nameof(stream);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NullPatternAfterDispose_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<UseAfterDisposeAnalyzer>("""
                           using System.IO;
                           class C
                           {
                               bool M()
                               {
                                   var stream = new MemoryStream();
                                   stream.Dispose();
                                   return stream is null;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DisposeInIfReadInElse_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<UseAfterDisposeAnalyzer>("""
                           using System.IO;
                           class C
                           {
                               void M(bool c)
                               {
                                   var stream = new MemoryStream();
                                   if (c)
                                   {
                                       stream.Dispose();
                                   }
                                   else
                                   {
                                       stream.WriteByte(1);
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DisposeInSwitchSectionReadInOtherSection_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<UseAfterDisposeAnalyzer>("""
                           using System.IO;
                           class C
                           {
                               void M(int n)
                               {
                                   var stream = new MemoryStream();
                                   switch (n)
                                   {
                                       case 0:
                                           stream.Dispose();
                                           break;
                                       default:
                                           stream.WriteByte(1);
                                           break;
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ReadInLaterNestedStatement_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<UseAfterDisposeAnalyzer>("""
                           using System.IO;
                           class C
                           {
                               void M(bool c)
                               {
                                   var stream = new MemoryStream();
                                   stream.Dispose();
                                   if (c)
                                   {
                                       {|E128162:stream|}.WriteByte(1);
                                   }
                               }
                           }
                           """);
    }
}

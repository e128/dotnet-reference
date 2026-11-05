using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class DisposableFieldInLoopAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<DisposableFieldInLoopAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ForeachOverwrite_WithNoDispose_Fires()
    {
        return VerifyAsync("""
                           using System;
                           class Res : IDisposable
                           {
                               public void Dispose()
                               {
                               }
                           }
                           class C
                           {
                               private IDisposable? _current;
                               public void Run(string[] items)
                               {
                                   foreach (var item in items)
                                   {
                                       {|E128193:_current|} = new Res();
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ForeachOverwrite_WithThisQualifiedDisposeBeforeTheLoop_Fires()
    {
        return VerifyAsync("""
                           using System;
                           class Res : IDisposable
                           {
                               public void Dispose()
                               {
                               }
                           }
                           class C
                           {
                               private IDisposable? _current;
                               public void Run(string[] items)
                               {
                                   this._current?.Dispose();
                                   foreach (var item in items)
                                   {
                                       {|E128193:_current|} = new Res();
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ForeachOverwrite_WithOnlyADisposeAfterTheAssignment_Fires()
    {
        return VerifyAsync("""
                           using System;
                           class Res : IDisposable
                           {
                               public void Dispose()
                               {
                               }
                           }
                           class C
                           {
                               private IDisposable? _current;
                               public void Run(string[] items)
                               {
                                   foreach (var item in items)
                                   {
                                       {|E128193:_current|} = new Res();
                                       _current?.Dispose();
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ForeachOverwrite_WithADisposeOnAnotherField_Fires()
    {
        return VerifyAsync("""
                           using System;
                           class Res : IDisposable
                           {
                               public void Dispose()
                               {
                               }
                           }
                           class C
                           {
                               private IDisposable? _current;
                               private IDisposable? _other;
                               public void Run(string[] items)
                               {
                                   _other?.Dispose();
                                   foreach (var item in items)
                                   {
                                       {|E128193:_current|} = new Res();
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AssignmentOutsideALoop_DoesNotFire()
    {
        return VerifyAsync("""
                           using System;
                           class Res : IDisposable
                           {
                               public void Dispose()
                               {
                               }
                           }
                           class C
                           {
                               private IDisposable? _current;
                               public void Set()
                               {
                                   _current = new Res();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NonDisposableFieldInsideALoop_DoesNotFire()
    {
        return VerifyAsync("""
                           using System;
                           class C
                           {
                               private string? _text;
                               public void Run(string[] items)
                               {
                                   foreach (var item in items)
                                   {
                                       _text = item;
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task WhileOverwrite_WithNoDispose_Fires()
    {
        return VerifyAsync("""
                           using System;
                           class Res : IDisposable
                           {
                               public void Dispose()
                               {
                               }
                           }
                           class C
                           {
                               private IDisposable? _current;
                               public void Run(int count)
                               {
                                   var index = 0;
                                   while (index < count)
                                   {
                                       {|E128193:_current|} = new Res();
                                       index++;
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PropertyAssignmentsInALoop_DoesNotFire()
    {
        return VerifyAsync("""
                           using System;
                           class Res : IDisposable
                           {
                               public void Dispose()
                               {
                               }
                           }
                           class C
                           {
                               private IDisposable? Current { get; set; }
                               public void Run(string[] items)
                               {
                                   foreach (var item in items)
                                   {
                                       Current = new Res();
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DisposeInTheLoopBeforeTheAssignment_DoesNotFire()
    {
        return VerifyAsync("""
                           using System;
                           class Res : IDisposable
                           {
                               public void Dispose()
                               {
                               }
                           }
                           class C
                           {
                               private IDisposable? _current;
                               public void Run(string[] items)
                               {
                                   foreach (var item in items)
                                   {
                                       _current?.Dispose();
                                       _current = new Res();
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConstrainedTypeParameterField_Fires()
    {
        return VerifyAsync("""
                           using System;
                           class C<T> where T : IDisposable, new()
                           {
                               private T _current = new();
                               public void Run(string[] items)
                               {
                                   foreach (var item in items)
                                   {
                                       {|E128193:_current|} = new T();
                                   }
                               }
                           }
                           """);
    }
}

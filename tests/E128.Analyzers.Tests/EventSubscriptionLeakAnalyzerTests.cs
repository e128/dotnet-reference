using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class EventSubscriptionLeakAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        return AnalyzerTestRunner.VerifyAsync<EventSubscriptionLeakAnalyzer>(code, expected);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SubscriptionToOwnInstanceEvent_DoesNotFire()
    {
        return VerifyAsync("""
                           using System;
                           class C : IDisposable
                           {
                               public event EventHandler? Changed;
                               private int _count;
                               public void Start()
                               {
                                   Changed += (s, e) => _count++;
                                   this.Changed += (s, e) => _count++;
                               }
                               public void Dispose()
                               {
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SubscriptionToOwnStaticEvent_Fires()
    {
        return VerifyAsync("""
                           using System;
                           class C : IDisposable
                           {
                               public static event EventHandler? Changed;
                               private int _count;
                               public void Start()
                               {
                                   {|E128176:Changed += (s, e) => _count++|};
                               }
                               public void Dispose()
                               {
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task RemovalInDisposeAsyncCore_DoesNotFire()
    {
        return VerifyAsync("""
                           using System;
                           using System.Threading.Tasks;
                           class Publisher
                           {
                               public event EventHandler? Changed;
                           }
                           class C : IAsyncDisposable
                           {
                               private readonly Publisher _publisher = new();
                               private int _count;
                               public void Start()
                               {
                                   _publisher.Changed += OnChanged;
                               }
                               public ValueTask DisposeAsync() => DisposeAsyncCore();
                               protected virtual ValueTask DisposeAsyncCore()
                               {
                                   _publisher.Changed -= OnChanged;
                                   return default;
                               }
                               private void OnChanged(object? sender, EventArgs e)
                               {
                                   _count++;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LambdaThatReadsAField_WithNoRemoval_Fires()
    {
        return VerifyAsync("""
                           using System;
                           class Publisher
                           {
                               public event EventHandler? Changed;
                           }
                           class C : IDisposable
                           {
                               private readonly Publisher _publisher = new();
                               private int _count;
                               public void Start()
                               {
                                   {|E128176:_publisher.Changed += (s, e) => _count++|};
                               }
                               public void Dispose()
                               {
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LambdaThatReadsAField_RemovedInDispose_DoesNotFire()
    {
        return VerifyAsync("""
                           using System;
                           class Publisher
                           {
                               public event EventHandler? Changed;
                           }
                           class C : IDisposable
                           {
                               private readonly Publisher _publisher = new();
                               private int _count;
                               public void Start()
                               {
                                   _publisher.Changed += (s, e) => _count++;
                               }
                               public void Dispose()
                               {
                                   _publisher.Changed -= (s, e) => _count++;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task StaticHandler_DoesNotFire()
    {
        return VerifyAsync("""
                           using System;
                           class Publisher
                           {
                               public event EventHandler? Changed;
                           }
                           class C : IDisposable
                           {
                               private readonly Publisher _publisher = new();
                               public void Start()
                               {
                                   _publisher.Changed += Handle;
                               }
                               public void Dispose()
                               {
                               }
                               private static void Handle(object? sender, EventArgs e)
                               {
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task InstanceMethodGroup_WithNoRemoval_Fires()
    {
        return VerifyAsync("""
                           using System;
                           class Publisher
                           {
                               public event EventHandler? Changed;
                           }
                           class C : IDisposable
                           {
                               private readonly Publisher _publisher = new();
                               public void Start()
                               {
                                   {|E128176:_publisher.Changed += Handle|};
                               }
                               public void Dispose()
                               {
                               }
                               private void Handle(object? sender, EventArgs e)
                               {
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ThisQualifiedMethodGroup_WithNoRemoval_Fires()
    {
        return VerifyAsync("""
                           using System;
                           class Publisher
                           {
                               public event EventHandler? Changed;
                           }
                           class C : IDisposable
                           {
                               private readonly Publisher _publisher = new();
                               public void Start()
                               {
                                   {|E128176:_publisher.Changed += this.Handle|};
                               }
                               public void Dispose()
                               {
                               }
                               private void Handle(object? sender, EventArgs e)
                               {
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TypeWithoutDispose_DoesNotFire()
    {
        return VerifyAsync("""
                           using System;
                           class Publisher
                           {
                               public event EventHandler? Changed;
                           }
                           class C
                           {
                               private readonly Publisher _publisher = new();
                               private int _count;
                               public void Start()
                               {
                                   _publisher.Changed += (s, e) => _count++;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LambdaWithoutAnInstanceRead_DoesNotFire()
    {
        return VerifyAsync("""
                           using System;
                           class Publisher
                           {
                               public event EventHandler? Changed;
                           }
                           class C : IDisposable
                           {
                               private readonly Publisher _publisher = new();
                               public void Start(int limit)
                               {
                                   _publisher.Changed += (s, e) => Console.WriteLine(limit);
                               }
                               public void Dispose()
                               {
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task RemovalInAnOrdinaryMethod_DoesNotFire()
    {
        return VerifyAsync("""
                           using System;
                           class Publisher
                           {
                               public event EventHandler? Changed;
                           }
                           class C : IDisposable
                           {
                               private readonly Publisher _publisher = new();
                               private int _count;
                               public void Start()
                               {
                                   _publisher.Changed += (s, e) => _count++;
                               }
                               public void Stop()
                               {
                                   _publisher.Changed -= (s, e) => _count++;
                               }
                               public void Dispose()
                               {
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task RemovalInAHelperCalledFromDispose_DoesNotFire()
    {
        return VerifyAsync("""
                           using System;
                           class Publisher
                           {
                               public event EventHandler? Changed;
                           }
                           class C : IDisposable
                           {
                               private readonly Publisher _publisher = new();
                               private int _count;
                               public void Start()
                               {
                                   _publisher.Changed += OnChanged;
                               }
                               public void Dispose()
                               {
                                   Unsubscribe();
                               }
                               private void Unsubscribe()
                               {
                                   _publisher.Changed -= OnChanged;
                               }
                               private void OnChanged(object? sender, EventArgs e)
                               {
                                   _count++;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task RemovalOnAnotherPublisherInstance_Fires()
    {
        return VerifyAsync("""
                           using System;
                           class Publisher
                           {
                               public event EventHandler? Changed;
                           }
                           class C : IDisposable
                           {
                               private readonly Publisher _first = new();
                               private readonly Publisher _second = new();
                               private int _count;
                               public void Start()
                               {
                                   _first.Changed += (s, e) => _count++;
                                   {|E128176:_second.Changed += (s, e) => _count++|};
                               }
                               public void Dispose()
                               {
                                   _first.Changed -= (s, e) => _count++;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task RemovalInDisposeAsync_DoesNotFire()
    {
        return VerifyAsync("""
                           using System;
                           using System.Threading.Tasks;
                           class Publisher
                           {
                               public event EventHandler? Changed;
                           }
                           class C : IAsyncDisposable
                           {
                               private readonly Publisher _publisher = new();
                               private int _count;
                               public void Start()
                               {
                                   _publisher.Changed += (s, e) => _count++;
                               }
                               public ValueTask DisposeAsync()
                               {
                                   _publisher.Changed -= (s, e) => _count++;
                                   return default;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task RemovalOfADifferentEvent_Fires()
    {
        return VerifyAsync("""
                           using System;
                           class Publisher
                           {
                               public event EventHandler? Changed;
                               public event EventHandler? Closed;
                           }
                           class C : IDisposable
                           {
                               private readonly Publisher _publisher = new();
                               private int _count;
                               public void Start()
                               {
                                   {|E128176:_publisher.Changed += (s, e) => _count++|};
                                   _publisher.Closed += (s, e) => _count++;
                               }
                               public void Dispose()
                               {
                                   _publisher.Closed -= (s, e) => _count++;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task OwnedPublisherFieldDisposedInDispose_DoesNotFire()
    {
        return VerifyAsync("""
                           using System;
                           class Publisher : IDisposable
                           {
                               public event EventHandler? Changed;
                               public void Dispose()
                               {
                               }
                           }
                           class C : IDisposable
                           {
                               private readonly Publisher _publisher = new();
                               private int _count;
                               public void Start()
                               {
                                   _publisher.Changed += (s, e) => _count++;
                               }
                               public void Dispose()
                               {
                                   _publisher.Dispose();
                               }
                           }
                           """);
    }
}

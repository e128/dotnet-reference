using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class RetainedMemoryAfterDisposeAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheMemoryIsReadAfterTheUsing()
    {
        return AnalyzerTestRunner.VerifyAsync<RetainedMemoryAfterDisposeAnalyzer>("""
                           using System;
                           using System.Buffers;

                           class C
                           {
                               void Run()
                               {
                                   Memory<byte> retained;
                                   using (var owner = MemoryPool<byte>.Shared.Rent(1))
                                   {
                                       retained = owner.Memory;
                                   }

                                   Consume([|retained|]);
                               }

                               void Consume(Memory<byte> memory) { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheSliceIsReadAfterTheUsing()
    {
        return AnalyzerTestRunner.VerifyAsync<RetainedMemoryAfterDisposeAnalyzer>("""
                           using System;
                           using System.Buffers;

                           class C
                           {
                               void Run()
                               {
                                   Memory<byte> retained;
                                   using (var owner = MemoryPool<byte>.Shared.Rent(2))
                                   {
                                       retained = owner.Memory.Slice(0, 1);
                                   }

                                   Consume([|retained|]);
                               }

                               void Consume(Memory<byte> memory) { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheReadIsInsideTheUsing()
    {
        return AnalyzerTestRunner.VerifyAsync<RetainedMemoryAfterDisposeAnalyzer>("""
                           using System;
                           using System.Buffers;

                           class C
                           {
                               void Run()
                               {
                                   using (var owner = MemoryPool<byte>.Shared.Rent(1))
                                   {
                                       var retained = owner.Memory;
                                       Consume(retained);
                                   }
                               }

                               void Consume(Memory<byte> memory) { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheOwnerIsNotAMemoryOwner()
    {
        return AnalyzerTestRunner.VerifyAsync<RetainedMemoryAfterDisposeAnalyzer>("""
                           using System;

                           class C
                           {
                               void Run()
                               {
                                   Memory<byte> retained;
                                   using (var disposable = new Disposable())
                                   {
                                       retained = default;
                                   }

                                   Consume(retained);
                               }

                               void Consume(Memory<byte> memory) { }
                           }

                           sealed class Disposable : IDisposable
                           {
                               public void Dispose() { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheOwnerDerivesFromMemoryManager()
    {
        return AnalyzerTestRunner.VerifyAsync<RetainedMemoryAfterDisposeAnalyzer>("""
                           using System;
                           using System.Buffers;

                           abstract class Owner : MemoryManager<byte>
                           {
                               public override Span<byte> GetSpan() => default;
                               public override MemoryHandle Pin(int elementIndex = 0) => default;
                               public override void Unpin() { }
                               protected override void Dispose(bool disposing) { }
                           }

                           class C
                           {
                               void Run(Owner owner)
                               {
                                   Memory<byte> retained;
                                   using (owner)
                                   {
                                       retained = owner.Memory;
                                   }

                                   Consume([|retained|]);
                               }

                               void Consume(Memory<byte> memory) { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheRetainedLocalIsReassignedAfterTheUsing()
    {
        return AnalyzerTestRunner.VerifyAsync<RetainedMemoryAfterDisposeAnalyzer>("""
                           using System;
                           using System.Buffers;

                           class C
                           {
                               void Run()
                               {
                                   Memory<byte> retained;
                                   using (var owner = MemoryPool<byte>.Shared.Rent(1))
                                   {
                                       retained = owner.Memory;
                                   }

                                   retained = default;
                                   Consume(retained);
                               }

                               void Consume(Memory<byte> memory) { }
                           }
                           """);
    }
}

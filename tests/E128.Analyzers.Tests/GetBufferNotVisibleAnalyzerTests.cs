using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class GetBufferNotVisibleAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<GetBufferNotVisibleAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    #region Fires

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenByteArrayLocalIsCreatedWithoutTheFlag()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               void M()
                               {
                                   var stream = new MemoryStream(new byte[8]);
                                   var buffer = {|E128192:stream.GetBuffer()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenPubliclyVisibleIsFalse()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               void M()
                               {
                                   var stream = new MemoryStream(new byte[8], 0, 8, true, false);
                                   var buffer = {|E128192:stream.GetBuffer()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTargetTypedNewOmitsTheFlag()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               void M()
                               {
                                   MemoryStream stream = new(new byte[8]);
                                   var buffer = {|E128192:stream.GetBuffer()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenParameterIsAssignedInTheSameMethod()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               void M(MemoryStream stream)
                               {
                                   stream = new MemoryStream(new byte[8]);
                                   var buffer = {|E128192:stream.GetBuffer()|};
                               }
                           }
                           """);
    }

    #endregion Fires

    #region Does Not Fire

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenStreamIsCreatedEmpty()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               void M()
                               {
                                   var stream = new MemoryStream();
                                   var buffer = stream.GetBuffer();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenStreamIsCreatedWithACapacity()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               void M()
                               {
                                   var stream = new MemoryStream(16);
                                   var buffer = stream.GetBuffer();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenPubliclyVisibleIsTrue()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               void M()
                               {
                                   var stream = new MemoryStream(new byte[8], 0, 8, writable: true, publiclyVisible: true);
                                   var buffer = stream.GetBuffer();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenPubliclyVisibleIsNotConstant()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               void M(bool visible)
                               {
                                   var stream = new MemoryStream(new byte[8], 0, 8, true, visible);
                                   var buffer = stream.GetBuffer();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenParameterIsAssignedAnExposingStream()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               void M(MemoryStream stream)
                               {
                                   stream = new MemoryStream(new byte[8], 0, 8, true, true);
                                   var buffer = stream.GetBuffer();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenReceiverIsAField()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               private MemoryStream _stream;
                               void M()
                               {
                                   _stream = new MemoryStream();
                                   var buffer = _stream.GetBuffer();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenCreationIsInAFieldInitializer()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               private readonly MemoryStream _stream = new MemoryStream();
                               void M()
                               {
                                   var buffer = _stream.GetBuffer();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenCreationIsInADifferentMethod()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               private MemoryStream _stream;
                               void Initialize()
                               {
                                   _stream = new MemoryStream();
                               }
                               void Use()
                               {
                                   var buffer = _stream.GetBuffer();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenReceiverIsAMethodResult()
    {
        return VerifyAsync("""
                           using System.IO;
                           class C
                           {
                               MemoryStream Create() => new MemoryStream();
                               void M()
                               {
                                   var buffer = Create().GetBuffer();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenReceiverTypeIsNotMemoryStream()
    {
        return VerifyAsync("""
                           class Buffer
                           {
                               public byte[] GetBuffer() => new byte[0];
                           }
                           class C
                           {
                               void M()
                               {
                                   var buffer = new Buffer().GetBuffer();
                               }
                           }
                           """);
    }

    #endregion Does Not Fire
}

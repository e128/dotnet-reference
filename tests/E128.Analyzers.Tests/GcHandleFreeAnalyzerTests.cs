using System.Collections.Immutable;
using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class GcHandleFreeAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected) =>
        AnalyzerTestRunner.VerifyAsync<GcHandleFreeAnalyzer>(code, expected);

    /// <summary>
    ///     Removing every metadata reference makes GCHandle unresolvable, which is the fail-closed path the
    ///     analyzer takes when the type is absent.
    /// </summary>
    private static Task VerifyWithoutGCHandleAsync(string code)
    {
        var test = new CSharpAnalyzerTest<GcHandleFreeAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            CompilerDiagnostics = CompilerDiagnostics.None
        };
        test.SolutionTransforms.Add((solution, projectId) =>
            solution.WithProjectMetadataReferences(projectId, ImmutableArray<MetadataReference>.Empty));
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AllocAssignedToLocal_NotFreed_Fires()
    {
        return VerifyAsync("""
                           using System.Runtime.InteropServices;
                           class C
                           {
                               void M(object payload)
                               {
                                   var handle = {|E128189:GCHandle.Alloc(payload)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AllocAssignedAfterDeclaration_NotFreed_Fires()
    {
        return VerifyAsync("""
                           using System.Runtime.InteropServices;
                           class C
                           {
                               void M(object payload)
                               {
                                   GCHandle handle;
                                   handle = {|E128189:GCHandle.Alloc(payload)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AllocAssignedToLocal_Freed_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Runtime.InteropServices;
                           class C
                           {
                               void M(object payload)
                               {
                                   var handle = GCHandle.Alloc(payload);
                                   handle.Free();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AllocFreedInFinally_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Runtime.InteropServices;
                           class C
                           {
                               void M(object payload)
                               {
                                   var handle = GCHandle.Alloc(payload);
                                   try
                                   {
                                       payload.GetHashCode();
                                   }
                                   finally
                                   {
                                       handle.Free();
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AllocInsideALambdaFreedLaterInTheMethod_DoesNotFire()
    {
        return VerifyAsync("""
                           using System;
                           using System.Runtime.InteropServices;
                           class C
                           {
                               void M(object payload)
                               {
                                   GCHandle handle = default;
                                   Action allocate = () => handle = GCHandle.Alloc(payload);
                                   allocate();
                                   handle.Free();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AllocPassedToMethod_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Runtime.InteropServices;
                           class C
                           {
                               void M(object payload)
                               {
                                   var handle = GCHandle.Alloc(payload);
                                   Use(handle);
                               }

                               private static void Use(GCHandle value)
                               {
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AllocStoredInField_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Runtime.InteropServices;
                           class C
                           {
                               private GCHandle _stored;

                               void M(object payload)
                               {
                                   var handle = GCHandle.Alloc(payload);
                                   _stored = handle;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AllocResultAssignedToField_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Runtime.InteropServices;
                           class C
                           {
                               private GCHandle _handle;

                               void M(object payload)
                               {
                                   _handle = GCHandle.Alloc(payload);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AllocReturned_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Runtime.InteropServices;
                           class C
                           {
                               GCHandle M(object payload)
                               {
                                   var handle = GCHandle.Alloc(payload);
                                   return handle;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AllocStoredInCollection_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Runtime.InteropServices;
                           class C
                           {
                               void M(object payload)
                               {
                                   var handles = new List<GCHandle>();
                                   var handle = GCHandle.Alloc(payload);
                                   handles.Add(handle);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AllocDiscarded_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Runtime.InteropServices;
                           class C
                           {
                               void M(object payload)
                               {
                                   GCHandle.Alloc(payload);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task UserDefinedGCHandleType_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Runtime.InteropServices;
                           class GCHandle
                           {
                               public static GCHandle Alloc(object value)
                               {
                                   return null;
                               }
                           }
                           class C
                           {
                               void M(object payload)
                               {
                                   var handle = GCHandle.Alloc(payload);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CompilationWithoutGCHandle_DoesNotFire()
    {
        return VerifyWithoutGCHandleAsync("""
                                            class C
                                            {
                                                void M(object payload)
                                                {
                                                    var handle = GCHandle.Alloc(payload);
                                                }
                                            }
                                            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task BareAllocAfterUsingStatic_NotFreed_Fires()
    {
        return VerifyAsync("""
                           using static System.Runtime.InteropServices.GCHandle;
                           class C
                           {
                               void M(object payload)
                               {
                                   var handle = {|E128189:Alloc(payload)|};
                               }
                           }
                           """);
    }
}

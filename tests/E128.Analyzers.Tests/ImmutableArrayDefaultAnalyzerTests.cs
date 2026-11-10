using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ImmutableArrayDefaultAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<ImmutableArrayDefaultAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task UninitializedField_Fires()
    {
        return VerifyAsync("""
                           using System.Collections.Immutable;
                           class C
                           {
                               private ImmutableArray<int> {|E128163:values|};

                               internal int M() => values.Length;
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task InitializedField_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Collections.Immutable;
                           class C
                           {
                               private ImmutableArray<int> values = ImmutableArray<int>.Empty;

                               internal int M() => values.Length;
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FieldAssignedInConstructor_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Collections.Immutable;
                           class C
                           {
                               private ImmutableArray<int> values;

                               internal C()
                               {
                                   values = ImmutableArray.Create(1);
                               }

                               internal int M() => values.Length;
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PropertyWithoutInitializer_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Collections.Immutable;
                           class C
                           {
                               internal ImmutableArray<int> Values { get; set; }

                               internal int M() => Values.Length;
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task OtherFieldType_DoesNotFire()
    {
        return VerifyAsync("""
                           class C
                           {
                               private int[] values;

                               internal int M() => values.Length;
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DeconstructionAssignment_Silent()
    {
        return VerifyAsync("""
                           using System.Collections.Immutable;
                           class C
                           {
                               private ImmutableArray<int> values;

                               internal void Set(ImmutableArray<int> a, int n)
                               {
                                   (values, _) = (a, n);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task OutArgument_Silent()
    {
        return VerifyAsync("""
                           using System.Collections.Immutable;
                           class C
                           {
                               private ImmutableArray<int> values;

                               private static void Fill(out ImmutableArray<int> target)
                               {
                                   target = ImmutableArray<int>.Empty;
                               }

                               internal void Init()
                               {
                                   Fill(out values);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ImmutableInterlockedRefArgument_Silent()
    {
        return VerifyAsync("""
                           using System.Collections.Immutable;
                           class C
                           {
                               private ImmutableArray<int> values;

                               internal void Init()
                               {
                                   ImmutableInterlocked.InterlockedInitialize(ref values, ImmutableArray<int>.Empty);
                               }
                           }
                           """);
    }
}

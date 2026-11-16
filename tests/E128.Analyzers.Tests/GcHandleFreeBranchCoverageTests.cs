using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class GcHandleFreeBranchCoverageTests
{
    private static Task VerifyAsync(string members)
    {
        return AnalyzerTestRunner.VerifyAsync<GcHandleFreeAnalyzer>(
            "using System;\nusing System.Runtime.InteropServices;\nclass C\n{\n" + members + "}\n");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_InAConstructorAndADestructor()
    {
        return VerifyAsync("""
                public C()
                {
                    var handle = {|E128189:GCHandle.Alloc(new object())|};
                }

                ~C()
                {
                    var handle = {|E128189:GCHandle.Alloc(new object())|};
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_InAnAccessorAndOperators()
    {
        return VerifyAsync("""
                public object? Value
                {
                    get
                    {
                        var handle = {|E128189:GCHandle.Alloc(new object())|};
                        return null;
                    }
                }

                public static C operator +(C left, C right)
                {
                    var handle = {|E128189:GCHandle.Alloc(left)|};
                    return right;
                }

                public static implicit operator int(C value)
                {
                    var handle = {|E128189:GCHandle.Alloc(value)|};
                    return 0;
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForALambdaInsideAMethodThatNeverFrees()
    {
        return VerifyAsync("""
                void M()
                {
                    Action action = () =>
                    {
                        var handle = {|E128189:GCHandle.Alloc(new object())|};
                    };
                    action();
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForALambdaInAFieldInitializer()
    {
        return VerifyAsync("""
                Action action = () =>
                {
                    var handle = GCHandle.Alloc(new object());
                };

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheHandleIsCopiedOrStoredInAnInitializer()
    {
        return VerifyAsync("""
                void Copy()
                {
                    var handle = GCHandle.Alloc(new object());
                    var copy = handle;
                    copy.Free();
                }

                void Array()
                {
                    var handle = GCHandle.Alloc(new object());
                    var all = new[] { handle };
                    all[0].Free();
                }

                void Reassign()
                {
                    GCHandle other = default;
                    var handle = GCHandle.Alloc(new object());
                    other = handle;
                    other.Free();
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenOnlyAnotherHandleIsFreed()
    {
        return VerifyAsync("""
                void M(GCHandle other)
                {
                    var handle = {|E128189:GCHandle.Alloc(new object())|};
                    _ = handle.IsAllocated;
                    other.Free();
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAnAllocStoredInAnArrayElementOrAnotherType()
    {
        return VerifyAsync("""
                static class Pool
                {
                    public static object Alloc(object value) => value;
                }

                void M(GCHandle[] handles)
                {
                    handles[0] = GCHandle.Alloc(new object());
                    var other = Pool.Alloc(new object());
                    _ = other;
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAllocIsAnInvokedDelegateResult()
    {
        return VerifyAsync("""
                void M(Func<Func<object, GCHandle>> factory)
                {
                    var handle = factory()(new object());
                    handle.Free();
                }

            """);
    }
}

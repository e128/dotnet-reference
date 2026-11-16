using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class DisposableFieldInLoopAnalyzerBranchCoverageGapTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAConstrainedGenericFieldIsOverwritten()
    {
        return AnalyzerTestRunner.VerifyAsync<DisposableFieldInLoopAnalyzer>(
            """
            using System;
            class C<T> where T : IDisposable
            {
                private T value;
                void M(T replacement)
                {
                    while (true)
                    {
                        {|E128193:value|} = replacement;
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAnUnconstrainedGenericFieldIsOverwritten()
    {
        return AnalyzerTestRunner.VerifyAsync<DisposableFieldInLoopAnalyzer>(
            """
            class C<T>
            {
                private T value;
                void M(T replacement)
                {
                    while (true)
                    {
                        value = replacement;
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenADisposableStructFieldIsOverwritten()
    {
        return AnalyzerTestRunner.VerifyAsync<DisposableFieldInLoopAnalyzer>(
            """
            using System;
            struct Item : IDisposable { public void Dispose() { } }
            class C
            {
                private Item value;
                void M(Item replacement)
                {
                    while (true)
                    {
                        value = replacement;
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenOnlyAParameterIsDisposedBeforeReplacement()
    {
        return AnalyzerTestRunner.VerifyAsync<DisposableFieldInLoopAnalyzer>(
            """
            using System;
            class C
            {
                private IDisposable value;
                void M(IDisposable replacement)
                {
                    while (true)
                    {
                        replacement.Dispose();
                        {|E128193:value|} = replacement;
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenOnlyAParameterIsDisposedAfterReplacement()
    {
        return AnalyzerTestRunner.VerifyAsync<DisposableFieldInLoopAnalyzer>(
            """
            using System;
            class C
            {
                private IDisposable value;
                void M(IDisposable replacement)
                {
                    while (true)
                    {
                        {|E128193:value|} = replacement;
                        replacement.Dispose();
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAConcreteDisposableFieldIsOverwritten()
    {
        return AnalyzerTestRunner.VerifyAsync<DisposableFieldInLoopAnalyzer>(
            """
            using System.IO;
            class C
            {
                private MemoryStream value;
                void M(MemoryStream replacement)
                {
                    while (true)
                    {
                        {|E128193:value|} = replacement;
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAGenericConstraintIsNotDisposable()
    {
        return AnalyzerTestRunner.VerifyAsync<DisposableFieldInLoopAnalyzer>(
            """
            class Resource { }
            class C<T> where T : Resource
            {
                private T value;
                void M(T replacement)
                {
                    while (true)
                    {
                        value = replacement;
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAnOrdinaryCallPrecedesTheAssignment()
    {
        return AnalyzerTestRunner.VerifyAsync<DisposableFieldInLoopAnalyzer>(
            """
            using System;
            using System.IO;
            class C
            {
                private MemoryStream value;
                void M(MemoryStream replacement)
                {
                    while (true)
                    {
                        GC.KeepAlive(this);
                        {|E128193:value|} = replacement;
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenALambdaExitsBetweenTheAssignmentAndTheDispose()
    {
        return AnalyzerTestRunner.VerifyAsync<DisposableFieldInLoopAnalyzer>(
            """
            using System;
            using System.IO;
            class C
            {
                private MemoryStream value;
                void M(MemoryStream replacement)
                {
                    while (true)
                    {
                        value = replacement;
                        Action leave = () => { return; };
                        GC.KeepAlive(leave);
                        value.Dispose();
                    }
                }
            }
            """);
    }
}

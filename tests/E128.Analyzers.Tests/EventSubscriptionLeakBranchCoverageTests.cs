using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class EventSubscriptionLeakBranchCoverageTests
{
    private static Task VerifyAsync(string members)
    {
        return AnalyzerTestRunner.VerifyAsync<EventSubscriptionLeakAnalyzer>(
            "using System;\nclass Publisher : IDisposable\n{\n    public event EventHandler? Changed;\n    public static event EventHandler? Shared;\n"
            + "    public int Count;\n    public void Dispose() { }\n}\n"
            + "class C : IDisposable\n{\n    private readonly Publisher _publisher = new();\n    private int _count;\n    private static int s_total;\n"
            + members + "}\n");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForCompoundAssignmentsThatAreNotEvents()
    {
        return VerifyAsync("""
                public void Run()
                {
                    _count += 1;
                    _count -= 1;
                }

                public void Dispose() { }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenOnlyANestedTypeRemovesTheStaticEvent()
    {
        return VerifyAsync("""
                public void Start()
                {
                    {|E128176:Publisher.Shared += (s, e) => _count++|};
                }

                public void Dispose() { }

                private sealed class Cleaner
                {
                    public void Remove()
                    {
                        Publisher.Shared -= (s, e) => { };
                    }
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheHandlerReadsThisExplicitly()
    {
        return VerifyAsync("""
                private void Handle() { }

                public void Start()
                {
                    {|E128176:_publisher.Changed += (s, e) => this.Handle()|};
                }

                public void Dispose() { }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenANestedLambdaReadsInstanceState()
    {
        return VerifyAsync("""
                public void Start()
                {
                    {|E128176:_publisher.Changed += (s, e) => { Action inner = () => _count++; inner(); }|};
                }

                public void Dispose() { }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheHandlerReadsNoInstanceState()
    {
        return VerifyAsync("""
                public void Start(Publisher other)
                {
                    _publisher.Changed += (s, e) => other.Count++;
                    _publisher.Changed += (s, e) => { var local = 1; local++; };
                    _publisher.Changed += (s, e) => s_total++;
                    _publisher.Changed += (s, e) => Console.WriteLine(nameof(Publisher));
                }

                public void Dispose() { }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheDisposeCallSitsInALocalFunction()
    {
        return VerifyAsync("""
                public void Start()
                {
                    {|E128176:_publisher.Changed += (s, e) => _count++|};
                }

                public void Dispose()
                {
                    void Release() { _publisher.Dispose(); }
                    Release();
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheDisposeCallSitsInAPropertyAccessor()
    {
        return VerifyAsync("""
                public void Start()
                {
                    {|E128176:_publisher.Changed += (s, e) => _count++|};
                }

                private bool Closed
                {
                    get
                    {
                        _publisher.Dispose();
                        return true;
                    }
                }

                public void Dispose() { }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheDisposedPublisherIsAnOwnedField()
    {
        return VerifyAsync("""
                public void Start()
                {
                    _publisher.Changed += (s, e) => _count++;
                }

                public void Dispose()
                {
                    _publisher.Dispose();
                    Console.WriteLine();
                    ((IDisposable)this).ToString();
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheStaticEventIsRemovedThroughAnyReceiver()
    {
        return VerifyAsync("""
                public void Start()
                {
                    Publisher.Shared += (s, e) => _count++;
                }

                public void Dispose()
                {
                    Publisher.Shared -= (s, e) => _count++;
                }

            """);
    }
}

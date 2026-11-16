using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class EventSubscriptionLeakAnalyzerBranchCoverageMoreTests
{
    private static Task VerifyAsync(string members)
    {
        return AnalyzerTestRunner.VerifyAsync<EventSubscriptionLeakAnalyzer>(
            "using System;\nclass Publisher\n{\n    public event EventHandler? Changed;\n}\n"
            + "class C : IDisposable\n{\n    private readonly Publisher _publisher = new();\n    private int _count;\n    public event EventHandler? Own;\n"
            + members + "}\n");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheSameRemovalAppearsTwiceAndOwnEventsAreRemoved()
    {
        return VerifyAsync("""
                private void OnChanged(object? sender, EventArgs e)
                {
                    _count++;
                }

                public void Start()
                {
                    _publisher.Changed += OnChanged;
                }

                public void Dispose()
                {
                    _publisher.Changed -= OnChanged;
                    _publisher.Changed -= OnChanged;
                    Own -= OnChanged;
                }

            """);
    }
}

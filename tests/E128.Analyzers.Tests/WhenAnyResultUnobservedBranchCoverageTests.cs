using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class WhenAnyResultUnobservedBranchCoverageTests
{
    private static Task VerifyAsync(string body)
    {
        return AnalyzerTestRunner.VerifyAsync<WhenAnyResultUnobservedAnalyzer>(
            "using System;\nusing System.Threading.Tasks;\nusing static System.Threading.Tasks.Task;\n"
            + "class Helper { public static Task<Task> WhenAny(Task a, Task b) => Task.FromResult(a); }\n"
            + "class C\n{\n    Task<Task> _field = Task.FromResult<Task>(Task.CompletedTask);\n    Task<Task> Property { get; set; } = Task.FromResult<Task>(Task.CompletedTask);\n"
            + "    void M(Task a, Task b)\n    {\n" + body + "\n    }\n}\n");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAnUnqualifiedWhenAnyThatIsDiscarded()
    {
        return VerifyAsync("""
                    {|E128183:WhenAny(a, b)|};
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAUserMethodNamedWhenAny()
    {
        return VerifyAsync("""
                    Helper.WhenAny(a, b);
                    var unused = Helper.WhenAny(a, b);
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheResultFlowsIntoAFieldOrAProperty()
    {
        return VerifyAsync("""
                    _field = Task.WhenAny(a, b);
                    Property = Task.WhenAny(a, b);
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheResultIsAnArgumentOrReturned()
    {
        return VerifyAsync("""
                    Consume(Task.WhenAny(a, b));
                    var pending = Task.WhenAny(a, b);
                    Consume(pending);

                    static void Consume(Task<Task> task) { }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLocalIsDeclaredInAForInitializer()
    {
        return VerifyAsync("""
                    for (var pending = Task.WhenAny(a, b); false; )
                    {
                    }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenALocalIsNeverReadInABlockWithOtherDeclarations()
    {
        return VerifyAsync("""
                    var pending = {|E128183:Task.WhenAny(a, b)|};
                    {
                        var other = 1;
                    }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenALambdaReadsTheLocal()
    {
        return VerifyAsync("""
                    var pending = Task.WhenAny(a, b);
                    Action action = () => { _ = pending; };
                    action();
            """);
    }
}

using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class StaticReadonlyFrozenCollectionCodeFixProviderBranchCoverageGapTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return CodeFixTestRunner.RunAsync<StaticReadonlyFrozenCollectionAnalyzer, StaticReadonlyFrozenCollectionCodeFixProvider>(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_KeepsAnExplicitCreationExpression()
    {
        return VerifyFixAsync(
            """
            using System.Collections.Generic;
            class C
            {
                private static readonly {|E128027:HashSet<string> Tags = new HashSet<string> { "a", "b" }|};
            }
            """,
            """
            using System.Collections.Frozen;
            using System.Collections.Generic;
            class C
            {
                private static readonly FrozenSet<string> Tags = new HashSet<string> { "a", "b" }.ToFrozenSet();
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_WrapsAFactoryCallInitializer()
    {
        return VerifyFixAsync(
            """
            using System.Collections.Generic;
            class C
            {
                private static readonly {|E128027:Dictionary<string, int> Lookup = Build()|};

                private static Dictionary<string, int> Build() => new();
            }
            """,
            """
            using System.Collections.Frozen;
            using System.Collections.Generic;
            class C
            {
                private static readonly FrozenDictionary<string, int> Lookup = Build().ToFrozenDictionary();

                private static Dictionary<string, int> Build() => new();
            }
            """);
    }
}

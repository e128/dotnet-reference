using System.Threading.Tasks;
using E128.Analyzers.Design;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class MutableCollectionExposureAnalyzerBranchCoverageTests
{
    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<MutableCollectionExposureAnalyzer>(code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAnInterfaceImplementationThatMustReturnTheMutableType()
    {
        return VerifyAsync("""
            using System.Collections.Generic;

            interface ISource
            {
                List<int> Items();
            }

            class Source : ISource
            {
                public List<int> Items() => new();
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForNonGenericAndUnmappedGenericTypes()
    {
        return VerifyAsync("""
            using System.Collections.Generic;

            class Source
            {
                public int Value() => 1;
                public int Count => 1;
                public string Name => "x";
                public IEnumerable<int> Items() => new int[0];
                public IEnumerable<int> Values => new int[0];
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAccessorsAndOverridesThatAreNotApiCandidates()
    {
        return VerifyAsync("""
            using System.Collections.Generic;

            abstract class Base
            {
                public abstract List<int> Items();
                public abstract List<int> Values { get; set; }
            }

            class Source : Base
            {
                public override List<int> Items() => new();
                public override List<int> Values { get; set; } = new();
                private List<int> PrivateItems() => new();
                protected List<int> ProtectedItems() => new();
                private List<int> PrivateValues => new();
                protected List<int> ProtectedValues => new();
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForInternalMembersAndInitOnlyProperties()
    {
        return VerifyAsync("""
            using System.Collections.Generic;

            class Source
            {
                internal List<int> {|E128052:Items|}() => new();
                internal Dictionary<string, int> {|E128052:Counts|} { get; init; } = new();
            }
            """);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("Builder")]
    [InlineData("State")]
    [InlineData("Stats")]
    [InlineData("Accumulator")]
    [InlineData("Options")]
    public Task Silent_ForTypesWhoseNamesDescribeMutableState(string suffix)
    {
        return VerifyAsync("""
            using System.Collections.Generic;
            class Some
            """ + suffix + """
             { public List<int> Items() => new(); public List<int> Values { get; } = new(); }
            """);
    }
}

using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class DuplicateJsonPropertyNameBranchCoverageTests
{
    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<DuplicateJsonPropertyNameAnalyzer>(
            "using System;\nusing System.Text.Json.Serialization;\n" + code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForADuplicateInAStruct()
    {
        return VerifyAsync("""
            public struct Widget
            {
                [JsonPropertyName("id")]
                public int First { get; set; }

                [{|E128178:JsonPropertyName("id")|}]
                public int Second { get; set; }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForInterfacesAndEnums()
    {
        return VerifyAsync("""
            public interface IWidget
            {
                [JsonPropertyName("id")]
                int First { get; }

                [JsonPropertyName("id")]
                int Second { get; }
            }

            public enum Kind { A, B }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForATypeWithoutAnyJsonPropertyName()
    {
        return VerifyAsync("""
            public sealed class Widget
            {
                public int First { get; set; }

                [Obsolete]
                public int Second { get; set; }

                public void Method() { }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAnotherAttributePrecedesTheJsonPropertyName()
    {
        return VerifyAsync("""
            public sealed class Widget
            {
                [Obsolete]
                [JsonPropertyName("id")]
                public int First { get; set; }

                [Obsolete]
                [{|E128178:JsonPropertyName("id")|}]
                public int Second { get; set; }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheJsonNameIsEmpty()
    {
        return VerifyAsync("""
            public sealed class Widget
            {
                [JsonPropertyName("")]
                public int First { get; set; }

                [JsonPropertyName("")]
                public int Second { get; set; }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForIndexersAndExplicitInterfaceImplementations()
    {
        return VerifyAsync("""
            public interface IHasId { int Id { get; } }

            public sealed class Widget : IHasId
            {
                [JsonPropertyName("id")]
                public int First { get; set; }

                [JsonPropertyName("id")]
                public int this[int index] => index;

                [JsonPropertyName("id")]
                int IHasId.Id => 1;
            }
            """);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("[JsonIgnore]")]
    [InlineData("[JsonIgnore(Condition = JsonIgnoreCondition.Always)]")]
    public Task Silent_WhenTheDuplicateMemberIsAlwaysIgnored(string ignore)
    {
        return VerifyAsync($$"""
            public sealed class Widget
            {
                [JsonPropertyName("id")]
                public int First { get; set; }

                {{ignore}}
                [JsonPropertyName("id")]
                public int Second { get; set; }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheIgnoreConditionIsNever()
    {
        return VerifyAsync("""
            public sealed class Widget
            {
                [JsonPropertyName("id")]
                public int First { get; set; }

                [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
                [{|E128178:JsonPropertyName("id")|}]
                public int Second { get; set; }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenANonPublicMemberHasJsonIncludeAndAnotherAttribute()
    {
        return VerifyAsync("""
            public sealed class Widget
            {
                [JsonPropertyName("id")]
                public int First { get; set; }

                [Obsolete]
                [JsonInclude]
                [{|E128178:JsonPropertyName("id")|}]
                internal int Second { get; set; }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_AgainstTheNearestBaseTypeThatDeclaresTheName()
    {
        return VerifyAsync("""
            public class Grandparent
            {
                [JsonPropertyName("id")]
                public int GrandparentId { get; set; }
            }

            public class Parent : Grandparent
            {
                [{|E128178:JsonPropertyName("id")|}]
                public int ParentId { get; set; }
            }

            public sealed class Child : Parent
            {
                [{|E128178:JsonPropertyName("id")|}]
                public int ChildId { get; set; }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAnOverrideReusesTheBaseJsonName()
    {
        return VerifyAsync("""
            public class Base
            {
                [JsonPropertyName("id")]
                public virtual int Id { get; set; }
            }

            public sealed class Derived : Base
            {
                [JsonPropertyName("id")]
                public override int Id { get; set; }
            }
            """);
    }
}

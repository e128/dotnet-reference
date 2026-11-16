using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class DuplicateJsonPropertyNameBranchCoverageGapTests
{
    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<DuplicateJsonPropertyNameAnalyzer>(
            "using System;\nusing System.Text.Json.Serialization;\n" + code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenADerivedFieldReusesTheNameOfABaseField()
    {
        return VerifyAsync("""
            public class Base
            {
                [JsonPropertyName("id")]
                public int BaseId;
            }

            public sealed class Derived : Base
            {
                [{|E128178:JsonPropertyName("id")|}]
                public int DerivedId;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAnUnresolvedAttributePrecedesTheJsonPropertyName()
    {
        return VerifyAsync("""
            public sealed class Widget
            {
                [JsonPropertyName("id")]
                public int First { get; set; }

                [{|CS0246:{|CS0246:Missing|}|}]
                [{|E128178:JsonPropertyName("id")|}]
                public int Second { get; set; }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheJsonNameIsNull()
    {
        return VerifyAsync("""
            public sealed class Widget
            {
                [JsonPropertyName(null)]
                public int First { get; set; }

                [JsonPropertyName(null)]
                public int Second { get; set; }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAJsonPropertyNameLookalikeCarriesNoName()
    {
        return VerifyAsync("""
            namespace System.Text.Json.Serialization
            {
                [AttributeUsage(AttributeTargets.Property)]
                public sealed class JsonPropertyNameAttribute : Attribute
                {
                    public JsonPropertyNameAttribute() { }

                    public JsonPropertyNameAttribute(string name) { }
                }
            }

            public sealed class Widget
            {
                [JsonPropertyName]
                public int First { get; set; }

                [JsonPropertyName]
                public int Second { get; set; }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAJsonIgnoreLookalikeNamesNoCondition()
    {
        return VerifyAsync("""
            namespace System.Text.Json.Serialization
            {
                [AttributeUsage(AttributeTargets.Property)]
                public sealed class JsonIgnoreAttribute : Attribute
                {
                    public int Order { get; set; }
                }
            }

            public sealed class Widget
            {
                [JsonPropertyName("id")]
                public int First { get; set; }

                [JsonIgnore(Order = 1)]
                [JsonPropertyName("id")]
                public int Second { get; set; }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAJsonIgnoreLookalikeNamesANonIntegerCondition()
    {
        return VerifyAsync("""
            namespace System.Text.Json.Serialization
            {
                [AttributeUsage(AttributeTargets.Property)]
                public sealed class JsonIgnoreAttribute : Attribute
                {
                    public string Condition { get; set; }
                }
            }

            public sealed class Widget
            {
                [JsonPropertyName("id")]
                public int First { get; set; }

                [JsonIgnore(Condition = "never")]
                [{|E128178:JsonPropertyName("id")|}]
                public int Second { get; set; }
            }
            """);
    }
}

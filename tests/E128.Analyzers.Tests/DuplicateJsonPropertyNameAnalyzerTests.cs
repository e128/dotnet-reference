using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class DuplicateJsonPropertyNameAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        return VerifyAsync(ReferenceAssemblies.Net.Net100, code, expected);
    }

    private static Task VerifyAsync(
        ReferenceAssemblies references,
        string code,
        params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<DuplicateJsonPropertyNameAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = references
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TwoPropertiesWithSameName_FiresOnSecond()
    {
        return VerifyAsync("""
                           using System.Text.Json.Serialization;

                           public sealed class Widget
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
    public Task NonPublicMemberWithSameName_IsSilent()
    {
        return VerifyAsync("""
                           using System.Text.Json.Serialization;

                           public sealed class Widget
                           {
                               [JsonPropertyName("id")]
                               public int First { get; set; }

                               [JsonPropertyName("id")]
                               private int Second { get; set; }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NonPublicMemberWithJsonInclude_FiresOnSecond()
    {
        return VerifyAsync("""
                           using System.Text.Json.Serialization;

                           public sealed class Widget
                           {
                               [JsonPropertyName("id")]
                               public int First { get; set; }

                               [{|E128178:JsonPropertyName("id")|}]
                               [JsonInclude]
                               private int Second { get; set; }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConditionalJsonIgnore_KeepsTheMemberInTheContract()
    {
        return VerifyAsync("""
                           using System.Text.Json.Serialization;

                           public sealed class Widget
                           {
                               [JsonPropertyName("id")]
                               public int First { get; set; }

                               [{|E128178:JsonPropertyName("id")|}]
                               [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
                               public int? Second { get; set; }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NewMemberHidingTheBaseMember_IsSilent()
    {
        return VerifyAsync("""
                           using System.Text.Json.Serialization;

                           public class Base
                           {
                               [JsonPropertyName("id")]
                               public int Id { get; set; }
                           }

                           public sealed class Derived : Base
                           {
                               [JsonPropertyName("id")]
                               public new int Id { get; set; }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ThreePropertiesWithSameName_FiresOnSecondAndThird()
    {
        return VerifyAsync("""
                           using System.Text.Json.Serialization;

                           public sealed class Widget
                           {
                               [JsonPropertyName("id")]
                               public int First { get; set; }

                               [{|E128178:JsonPropertyName("id")|}]
                               public int Second { get; set; }

                               [{|E128178:JsonPropertyName("id")|}]
                               public int Third { get; set; }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DerivedMemberReusesBaseName_FiresOnDerivedMember()
    {
        return VerifyAsync("""
                           using System.Text.Json.Serialization;

                           public class Base
                           {
                               [JsonPropertyName("id")]
                               public int BaseId { get; set; }
                           }

                           public sealed class Derived : Base
                           {
                               [{|E128178:JsonPropertyName("id")|}]
                               public int DerivedId { get; set; }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TwoFieldsWithSameName_FiresOnSecond()
    {
        return VerifyAsync("""
                           using System.Text.Json.Serialization;

                           public sealed class Widget
                           {
                               [JsonPropertyName("id")]
                               public int First;

                               [{|E128178:JsonPropertyName("id")|}]
                               public int Second;
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DifferentNames_NoFire()
    {
        return VerifyAsync("""
                           using System.Text.Json.Serialization;

                           public sealed class Widget
                           {
                               [JsonPropertyName("first")]
                               public int First { get; set; }

                               [JsonPropertyName("second")]
                               public int Second { get; set; }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NamesDifferOnlyByCase_NoFire()
    {
        return VerifyAsync("""
                           using System.Text.Json.Serialization;

                           public sealed class Widget
                           {
                               [JsonPropertyName("id")]
                               public int First { get; set; }

                               [JsonPropertyName("Id")]
                               public int Second { get; set; }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task StaticAndIgnoredMembers_NoFire()
    {
        return VerifyAsync("""
                           using System.Text.Json.Serialization;

                           public sealed class Widget
                           {
                               [JsonPropertyName("id")]
                               public static int First { get; set; }

                               [JsonPropertyName("id")]
                               public static int Second { get; set; }

                               [JsonPropertyName("name")]
                               public int Name { get; set; }

                               [JsonIgnore]
                               [JsonPropertyName("name")]
                               public int IgnoredName { get; set; }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AttributeTypeAbsent_NoFire()
    {
        // netstandard2.0 has no System.Text.Json, so the rule turns itself off. The lookalike
        // attribute below shares the name but not the symbol, and it must not wake the rule.
        return VerifyAsync(
            ReferenceAssemblies.NetStandard.NetStandard20,
            """
            namespace MyApp
            {
                [System.AttributeUsage(System.AttributeTargets.Property | System.AttributeTargets.Field)]
                public sealed class JsonPropertyNameAttribute : System.Attribute
                {
                    public JsonPropertyNameAttribute(string name)
                    {
                        Name = name;
                    }

                    public string Name { get; }
                }

                public sealed class Widget
                {
                    [JsonPropertyName("id")]
                    public int First { get; set; }

                    [JsonPropertyName("id")]
                    public int Second { get; set; }
                }
            }
            """);
    }
}

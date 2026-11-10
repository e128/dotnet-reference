using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class JsonSerializerOptionsMutationAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        return AnalyzerTestRunner.VerifyAsync<JsonSerializerOptionsMutationAnalyzer>(code, expected);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PropertyWriteAfterSerialize_Fires()
    {
        return VerifyAsync("""
                           using System.Text.Json;

                           class C
                           {
                               static string M(object value)
                               {
                                   var options = new JsonSerializerOptions();
                                   var json = JsonSerializer.Serialize(value, options);
                                   {|E128196:options.WriteIndented = true|};
                                   return json;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConvertersAddAfterDeserialize_Fires()
    {
        return VerifyAsync("""
                           using System.Text.Json;

                           class C
                           {
                               static object M(string json)
                               {
                                   var options = new JsonSerializerOptions();
                                   var value = JsonSerializer.Deserialize<object>(json, options);
                                   {|E128196:options.Converters.Clear()|};
                                   return value;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FieldWriteAfterSerialize_Fires()
    {
        return VerifyAsync("""
                           using System.Text.Json;

                           class C
                           {
                               private static readonly JsonSerializerOptions Options = new();

                               static string M(object value)
                               {
                                   var json = JsonSerializer.Serialize(value, Options);
                                   {|E128196:Options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase|};
                                   return json;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SerializeInsideLambdaBeforeWrite_DoesNotFire()
    {
        return VerifyAsync("""
                           using System;
                           using System.Text.Json;

                           class C
                           {
                               static Func<string> M(object value)
                               {
                                   var options = new JsonSerializerOptions();
                                   Func<string> later = () => JsonSerializer.Serialize(value, options);
                                   options.WriteIndented = true;
                                   return later;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SerializeAndWriteInSeparateBranches_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Text.Json;

                           class C
                           {
                               static string M(object value, bool compact)
                               {
                                   var options = new JsonSerializerOptions();
                                   if (compact)
                                   {
                                       return JsonSerializer.Serialize(value, options);
                                   }
                                   else
                                   {
                                       options.WriteIndented = true;
                                       return string.Empty;
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PropertyWriteBeforeUse_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Text.Json;

                           class C
                           {
                               static string M(object value)
                               {
                                   var options = new JsonSerializerOptions();
                                   options.WriteIndented = true;
                                   return JsonSerializer.Serialize(value, options);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PropertyWriteWithoutUse_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Text.Json;

                           class C
                           {
                               static void M()
                               {
                                   var options = new JsonSerializerOptions();
                                   options.WriteIndented = true;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task WriteToOtherInstance_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Text.Json;

                           class C
                           {
                               static string M(object value)
                               {
                                   var used = new JsonSerializerOptions();
                                   var other = new JsonSerializerOptions();
                                   var json = JsonSerializer.Serialize(value, used);
                                   other.WriteIndented = true;
                                   return json;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PropertyReceiverWrite_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Text.Json;

                           class C
                           {
                               private static JsonSerializerOptions Options { get; } = new();

                               static string M(object value)
                               {
                                   var json = JsonSerializer.Serialize(value, Options);
                                   Options.WriteIndented = true;
                                   return json;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task WriteInNestedLambda_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Text.Json;

                           class C
                           {
                               static string M(object value)
                               {
                                   var options = new JsonSerializerOptions();
                                   var json = JsonSerializer.Serialize(value, options);
                                   System.Action write = () => options.WriteIndented = true;
                                   write();
                                   return json;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task UnrelatedSerializerCall_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Text.Json;

                           class Serializer
                           {
                               internal static string Serialize(object value, JsonSerializerOptions options)
                               {
                                   return string.Empty;
                               }
                           }

                           class C
                           {
                               static string M(object value)
                               {
                                   var options = new JsonSerializerOptions();
                                   var json = Serializer.Serialize(value, options);
                                   options.WriteIndented = true;
                                   return json;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task UserTypeWithSameMemberNames_DoesNotFire()
    {
        return VerifyAsync("""
                           class Options
                           {
                               internal bool WriteIndented { get; set; }
                           }

                           class Serializer
                           {
                               internal static string Serialize(object value, Options options)
                               {
                                   return string.Empty;
                               }
                           }

                           class C
                           {
                               static string M(object value)
                               {
                                   var options = new Options();
                                   var json = Serializer.Serialize(value, options);
                                   options.WriteIndented = true;
                                   return json;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task WriteAfterReassignment_Silent()
    {
        return VerifyAsync("""
                           using System.Text.Json;

                           class C
                           {
                               static string M(object value)
                               {
                                   var options = new JsonSerializerOptions();
                                   var json = JsonSerializer.Serialize(value, options);
                                   options = new JsonSerializerOptions();
                                   options.WriteIndented = true;
                                   return json;
                               }
                           }
                           """);
    }
}

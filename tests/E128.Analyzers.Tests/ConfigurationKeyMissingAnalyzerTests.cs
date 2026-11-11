using System.Collections.Generic;
using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ConfigurationKeyMissingAnalyzerTests
{
    private const string ConfigurationStub = """
                                             namespace Microsoft.Extensions.Configuration
                                             {
                                                 public interface IConfiguration
                                                 {
                                                     string? this[string key] { get; set; }
                                                     IConfigurationSection GetSection(string key);
                                                 }

                                                 public interface IConfigurationSection : IConfiguration
                                                 {
                                                 }

                                                 public static class ConfigurationExtensions
                                                 {
                                                     public static IConfigurationSection GetRequiredSection(
                                                         this IConfiguration configuration,
                                                         string key)
                                                     {
                                                         return configuration.GetSection(key);
                                                     }

                                                     public static string? GetConnectionString(
                                                         this IConfiguration configuration,
                                                         string name)
                                                     {
                                                         return null;
                                                     }
                                                 }
                                             }
                                             """;

    private static Task VerifyAsync(
        string code,
        string? appSettings = null,
        string? addedKeys = null,
        (string Name, string Text)? extraFile = null,
        params DiagnosticResult[] expected)
    {
        var files = new List<(string Name, string Content)>();
        if (appSettings is not null)
        {
            files.Add(("appsettings.json", appSettings));
        }

        if (extraFile is { } file)
        {
            files.Add((file.Name, file.Text));
        }

        return AnalyzerTestRunner.VerifyAsync<ConfigurationKeyMissingAnalyzer>(
            code + ConfigurationStub,
            expected,
            options: new AnalyzerTestOptions
            {
                AnalyzerConfig = addedKeys is null
                    ? null
                    : ("/.editorconfig", "is_global = true\ne128_added_configuration_keys = " + addedKeys),
                AdditionalFiles = files
            });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task RootKeyPresent_IsSilent()
    {
        const string code = """
                            using Microsoft.Extensions.Configuration;

                            class C
                            {
                                static string? M(IConfiguration configuration)
                                {
                                    return configuration["Logging"];
                                }
                            }
                            """;

        return VerifyAsync(
            code,
            //lang=json
            """
            { "Logging": "Information" }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NestedKeyPresent_IsSilent()
    {
        const string code = """
                            using Microsoft.Extensions.Configuration;

                            class C
                            {
                                static string? M(IConfiguration configuration)
                                {
                                    return configuration["Logging:LogLevel:Default"];
                                }
                            }
                            """;

        return VerifyAsync(
            code,
            //lang=json
            """
            { "Logging": { "LogLevel": { "Default": "Warning" } } }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CompoundAssignment_StillFires()
    {
        const string code = """
                            using Microsoft.Extensions.Configuration;

                            class C
                            {
                                static void M(IConfiguration configuration)
                                {
                                    {|E128195:configuration["Logging:Missing"]|} += "x";
                                }
                            }
                            """;

        return VerifyAsync(
            code,
            //lang=json
            """
            { "Logging": { "LogLevel": { "Default": "Warning" } } }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task MissingKey_Fires()
    {
        const string code = """
                            using Microsoft.Extensions.Configuration;

                            class C
                            {
                                static string? M(IConfiguration configuration)
                                {
                                    return {|E128195:configuration["Logging:Missing"]|};
                                }
                            }
                            """;

        return VerifyAsync(
            code,
            //lang=json
            """
            { "Logging": { "LogLevel": { "Default": "Warning" } } }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SectionPresent_IsSilent()
    {
        const string code = """
                            using Microsoft.Extensions.Configuration;

                            class C
                            {
                                static IConfigurationSection M(IConfiguration configuration)
                                {
                                    return configuration.GetSection("Logging:LogLevel");
                                }
                            }
                            """;

        return VerifyAsync(
            code,
            //lang=json
            """
            { "Logging": { "LogLevel": { "Default": "Warning" } } }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task MissingKey_InFileWithCommentsAndTrailingComma_Fires()
    {
        const string code = """
                            using Microsoft.Extensions.Configuration;

                            class C
                            {
                                static string? M(IConfiguration configuration)
                                {
                                    return {|E128195:configuration["Logging:Missing"]|};
                                }
                            }
                            """;

        return VerifyAsync(
            code,
            //lang=json
            """
            {
              // The loader accepts this comment.
              "Logging": { "Level": "Information", },
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SectionRelativeKey_IsSilent()
    {
        const string code = """
                            using Microsoft.Extensions.Configuration;

                            class C
                            {
                                static string? M(IConfiguration configuration)
                                {
                                    var section = configuration.GetSection("Logging");
                                    return section["Level"] + section.GetSection("Child")["Leaf"];
                                }
                            }
                            """;

        return VerifyAsync(
            code,
            //lang=json
            """
            { "Logging": { "Level": "Information" } }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task IndexerAssignment_IsSilent()
    {
        const string code = """
                            using Microsoft.Extensions.Configuration;

                            class C
                            {
                                static void M(IConfiguration configuration)
                                {
                                    configuration["Logging:Created"] = "value";
                                }
                            }
                            """;

        return VerifyAsync(
            code,
            //lang=json
            """
            { "Logging": { "Level": "Information" } }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task MissingRequiredSection_Fires()
    {
        const string code = """
                            using Microsoft.Extensions.Configuration;

                            class C
                            {
                                static IConfigurationSection M(IConfiguration configuration)
                                {
                                    return {|E128195:configuration.GetRequiredSection("Logging:Missing")|};
                                }
                            }
                            """;

        return VerifyAsync(
            code,
            //lang=json
            """
            { "Logging": { "Level": "Information" } }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task MissingSection_Fires()
    {
        const string code = """
                            using Microsoft.Extensions.Configuration;

                            class C
                            {
                                static IConfigurationSection M(IConfiguration configuration)
                                {
                                    return {|E128195:configuration.GetSection("Logging:Missing")|};
                                }
                            }
                            """;

        return VerifyAsync(
            code,
            //lang=json
            """
            { "Logging": { "LogLevel": { "Default": "Warning" } } }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConnectionStringPresent_IsSilent()
    {
        const string code = """
                            using Microsoft.Extensions.Configuration;

                            class C
                            {
                                static string? M(IConfiguration configuration)
                                {
                                    return configuration.GetConnectionString("Default");
                                }
                            }
                            """;

        return VerifyAsync(
            code,
            //lang=json
            """
            { "ConnectionStrings": { "Default": "Server=local" } }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConnectionStringMissing_Fires()
    {
        const string code = """
                            using Microsoft.Extensions.Configuration;

                            class C
                            {
                                static string? M(IConfiguration configuration)
                                {
                                    return {|E128195:configuration.GetConnectionString("Missing")|};
                                }
                            }
                            """;

        return VerifyAsync(
            code,
            //lang=json
            """
            { "ConnectionStrings": { "Default": "Server=local" } }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NonConstantKey_IsSilent()
    {
        const string code = """
                            using Microsoft.Extensions.Configuration;

                            class C
                            {
                                static string? M(IConfiguration configuration, string key)
                                {
                                    return configuration[key];
                                }
                            }
                            """;

        return VerifyAsync(
            code,
            //lang=json
            """
            { "Logging": "Information" }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoJsonAdditionalFile_IsSilent()
    {
        const string code = """
                            using Microsoft.Extensions.Configuration;

                            class C
                            {
                                static string? M(IConfiguration configuration)
                                {
                                    return configuration["Logging:Missing"];
                                }
                            }
                            """;

        return VerifyAsync(code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task KeyCaseDiffers_IsSilent()
    {
        const string code = """
                            using Microsoft.Extensions.Configuration;

                            class C
                            {
                                static string? M(IConfiguration configuration)
                                {
                                    return configuration["logging:loglevel:default"];
                                }
                            }
                            """;

        return VerifyAsync(
            code,
            //lang=json
            """
            { "Logging": { "LogLevel": { "Default": "Warning" } } }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task InvalidJson_IsSilent()
    {
        const string code = """
                            using Microsoft.Extensions.Configuration;

                            class C
                            {
                                static string? M(IConfiguration configuration)
                                {
                                    return configuration["Logging:Missing"];
                                }
                            }
                            """;

        return VerifyAsync(code, "{ not json");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task OtherReceiver_IsSilent()
    {
        const string code = """
                            using System.Collections.Generic;
                            using Microsoft.Extensions.Configuration;

                            class C
                            {
                                static string? M(IConfiguration configuration)
                                {
                                    Dictionary<string, string> map = new();
                                    return map["Logging:Missing"];
                                }
                            }
                            """;

        return VerifyAsync(
            code,
            //lang=json
            """
            { "Logging": "Information" }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ArrayElementPresent_IsSilent()
    {
        const string code = """
                            using Microsoft.Extensions.Configuration;

                            class C
                            {
                                static string? M(IConfiguration configuration)
                                {
                                    return configuration["Hosts:0"];
                                }
                            }
                            """;

        return VerifyAsync(
            code,
            //lang=json
            """
            { "Hosts": [ "one", "two" ] }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AddedKeysOption_SuppliesMissingKey()
    {
        const string code = """
                            using Microsoft.Extensions.Configuration;

                            class C
                            {
                                static string? M(IConfiguration configuration)
                                {
                                    return configuration["Logging:External"];
                                }
                            }
                            """;

        return VerifyAsync(
            code,
            //lang=json
            """
            { "Logging": { "Level": "Information" } }
            """,
            addedKeys: "Logging:External");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AddedKeysOption_WithoutParsedFile_DoesNotActivateTheRule()
    {
        const string code = """
                            using Microsoft.Extensions.Configuration;

                            class C
                            {
                                static string? M(IConfiguration configuration)
                                {
                                    return configuration["Logging:External"];
                                }
                            }
                            """;

        return VerifyAsync(
            code,
            "{ not json",
            addedKeys: "Logging:External");
    }

    /// <summary>
    ///     Builds a JSON pair from parts, so the test data is not one JSON-looking literal token.
    /// </summary>
    private static string JsonPair(string key, string value)
    {
        return "{ \"" + key + "\": " + value + " }";
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AppSettingsPrefixedFile_IsRead()
    {
        const string code = """
                            using Microsoft.Extensions.Configuration;

                            class C
                            {
                                static string? M(IConfiguration configuration)
                                {
                                    return {|E128195:configuration["Logging:Missing"]|};
                                }
                            }
                            """;

        return VerifyAsync(
            code,
            extraFile: ("appsettings.Development.json", JsonPair("Logging", JsonPair("Level", "\"Information\""))));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NonAppSettingsJsonFile_IsIgnored()
    {
        const string code = """
                            using Microsoft.Extensions.Configuration;

                            class C
                            {
                                static string? M(IConfiguration configuration)
                                {
                                    return {|E128195:configuration["Other"]|};
                                }
                            }
                            """;

        return VerifyAsync(
            code,
            //lang=json
            """
            { "Logging": "Information" }
            """,
            extraFile: ("config.json", JsonPair("Other", "\"value\"")));
    }
}

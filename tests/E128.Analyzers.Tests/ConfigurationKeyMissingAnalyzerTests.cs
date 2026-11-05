using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
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
        params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<ConfigurationKeyMissingAnalyzer, DefaultVerifier>
        {
            TestCode = code + ConfigurationStub,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };

        if (appSettings is not null)
        {
            test.TestState.AdditionalFiles.Add(("appsettings.json", SourceText.From(appSettings)));
        }

        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
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
}

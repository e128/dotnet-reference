using System.Threading;
using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ConfigurationKeyMissingAnalyzerBranchCoverageMoreTests
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

    //lang=json
    private const string AppSettings = """{ "Known": 1 }""";

    private static AnalyzerTestOptions Options => new() { AdditionalFiles = [("appsettings.json", AppSettings)] };

    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        return AnalyzerTestRunner.VerifyAsync<ConfigurationKeyMissingAnalyzer>(
            code + ConfigurationStub,
            expected,
            options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCompilationHasNoConfigurationType()
    {
        return AnalyzerTestRunner.VerifyAsync<ConfigurationKeyMissingAnalyzer>(
            """
            class C
            {
                string M(string[] values) => values[0];
            }
            """,
            options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAnIndexerTakesTwoArguments()
    {
        return VerifyAsync(
            """
            class C
            {
                int M(int[,] grid) => grid[0, 1];
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAnInvocationHasNoArguments()
    {
        return VerifyAsync(
            """
            class C
            {
                string M(object value) => value.ToString() ?? string.Empty;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheInvokedExpressionIsNotAMemberAccess()
    {
        return VerifyAsync(
            """
            class C
            {
                static int Helper(string key) => key.Length;

                int M() => Helper("Missing");
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenADynamicCallUsesAConfigurationMethodName()
    {
        return VerifyAsync(
            """
            class C
            {
                object? M(dynamic value) => value.GetSection("Missing");
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAnUnrelatedTypeDeclaresGetSection()
    {
        return VerifyAsync(
            """
            class Other
            {
                public string? GetSection(string key) => key;
                public string? GetRequiredSection(string key) => key;
                public string? GetConnectionString(string key) => key;
            }

            class C
            {
                void M(Other other)
                {
                    _ = other.GetSection("Missing");
                    _ = other.GetRequiredSection("Missing");
                    _ = other.GetConnectionString("Missing");
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task Reports_WhenAnAppSettingsFileHasNoText()
    {
        var tree = CSharpSyntaxTree.ParseText(
            """
            using Microsoft.Extensions.Configuration;
            class C
            {
                string? M(IConfiguration configuration) => configuration["Missing"];
            }
            """ + ConfigurationStub,
            cancellationToken: TestContext.Current.CancellationToken);
        var compilation = CSharpCompilation.Create(
            "Probe",
            [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var options = new AnalyzerOptions(
            [new UnreadableAdditionalText("appsettings.json", null), new UnreadableAdditionalText("appsettings.Dev.json", AppSettings)]);

        var diagnostics = await compilation
            .WithAnalyzers([new ConfigurationKeyMissingAnalyzer()], options)
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("E128195", diagnostic.Id);
    }

    private sealed class UnreadableAdditionalText : AdditionalText
    {
        private readonly SourceText? text;

        internal UnreadableAdditionalText(string path, string? content)
        {
            Path = path;
            text = content is null ? null : SourceText.From(content);
        }

        public override string Path { get; }

        public override SourceText? GetText(CancellationToken cancellationToken = default)
        {
            return text;
        }
    }
}

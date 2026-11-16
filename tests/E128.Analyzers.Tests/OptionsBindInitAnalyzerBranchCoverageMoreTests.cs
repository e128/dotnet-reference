using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class OptionsBindInitAnalyzerBranchCoverageMoreTests
{
    private const string Stubs = """
                                 namespace Microsoft.Extensions.Configuration
                                 {
                                     public interface IConfiguration
                                     {
                                         IConfigurationSection GetSection(string key);
                                     }

                                     public interface IConfigurationSection : IConfiguration
                                     {
                                     }
                                 }

                                 namespace Microsoft.Extensions.Options
                                 {
                                     public class OptionsBuilder<T> where T : class
                                     {
                                         public OptionsBuilder<T> Bind(Microsoft.Extensions.Configuration.IConfigurationSection section) => this;
                                     }
                                 }

                                 """;

    private static Task VerifyAsync(string source)
    {
        return AnalyzerTestRunner.VerifyAsync<OptionsBindInitAnalyzer>(Stubs + source);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheBoundTypeIsATypeParameter()
    {
        return VerifyAsync("""
                           class Setup
                           {
                               static void Configure<T>(
                                   Microsoft.Extensions.Options.OptionsBuilder<T> builder,
                                   Microsoft.Extensions.Configuration.IConfiguration config)
                                   where T : class
                               {
                                   builder.Bind(config.GetSection("Options"));
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheBindReceiverIsDynamic()
    {
        return VerifyAsync("""
                           class Setup
                           {
                               static void Configure(dynamic builder, Microsoft.Extensions.Configuration.IConfiguration config)
                               {
                                   builder.Bind(config.GetSection("Options"));
                               }
                           }
                           """);
    }
}

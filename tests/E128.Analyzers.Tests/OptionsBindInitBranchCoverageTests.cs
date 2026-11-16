using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

/// <summary>
///     Covers the Bind call shapes and options types that the init-only rule reads.
/// </summary>
public sealed class OptionsBindInitBranchCoverageTests
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
    public Task Reports_EachInitAccessorOfTheBoundType()
    {
        return VerifyAsync("""
                           public class Options
                           {
                               public string First { get; {|E128033:init|}; }

                               public int Second { get; {|E128033:init|}; }

                               public int Third { get; set; }
                           }

                           class Setup
                           {
                               void Configure(Microsoft.Extensions.Configuration.IConfiguration config)
                               {
                                   new Microsoft.Extensions.Options.OptionsBuilder<Options>()
                                       .Bind(config.GetSection("Options"));
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenThePositionalRecordSynthesizesTheInitAccessor()
    {
        return VerifyAsync("""
                           public record Options(string Name);

                           class Setup
                           {
                               void Configure(Microsoft.Extensions.Configuration.IConfiguration config)
                               {
                                   new Microsoft.Extensions.Options.OptionsBuilder<Options>()
                                       .Bind(config.GetSection("Options"));
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheBoundTypeComesFromMetadata()
    {
        return VerifyAsync("""
                           class Setup
                           {
                               void Configure(Microsoft.Extensions.Configuration.IConfiguration config)
                               {
                                   new Microsoft.Extensions.Options.OptionsBuilder<System.Net.Http.HttpClientHandler>()
                                       .Bind(config.GetSection("Handler"));
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenBindBelongsToAnotherType()
    {
        return VerifyAsync("""
                           public class Options
                           {
                               public string Name { get; init; } = string.Empty;
                           }

                           class Binder
                           {
                               public Binder Bind(Microsoft.Extensions.Configuration.IConfigurationSection section) => this;
                           }

                           class Setup
                           {
                               void Configure(Microsoft.Extensions.Configuration.IConfiguration config)
                               {
                                   new Binder().Bind(config.GetSection("Options"));
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenBindIsCalledWithoutAReceiver()
    {
        return VerifyAsync("""
                           public class Options
                           {
                               public string Name { get; init; } = string.Empty;
                           }

                           class Setup
                           {
                               void Bind(Microsoft.Extensions.Configuration.IConfigurationSection section)
                               {
                               }

                               void Configure(Microsoft.Extensions.Configuration.IConfiguration config)
                               {
                                   Bind(config.GetSection("Options"));
                               }
                           }
                           """);
    }
}

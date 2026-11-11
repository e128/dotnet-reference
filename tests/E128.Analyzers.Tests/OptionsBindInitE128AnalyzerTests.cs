using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class OptionsBindInitE128AnalyzerTests
{
    #region Fires

    [Fact]
    [Trait("Category", "CI")]
    public Task OptionsBindInit_PropertyWithInit_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<OptionsBindInitAnalyzer>("""
                           namespace Microsoft.Extensions.Configuration
                           {
                               public interface IConfiguration
                               {
                                   IConfigurationSection GetSection(string key);
                               }
                               public interface IConfigurationSection : IConfiguration { }
                           }

                           namespace Microsoft.Extensions.Options
                           {
                               public class OptionsBuilder<T> where T : class
                               {
                                   public OptionsBuilder<T> Bind(Microsoft.Extensions.Configuration.IConfigurationSection section) => this;
                               }
                           }

                           public class MyOptions
                           {
                               public string Name { get; {|E128033:init|}; }
                           }

                           class Setup
                           {
                               void Configure(Microsoft.Extensions.Configuration.IConfiguration config)
                               {
                                   new Microsoft.Extensions.Options.OptionsBuilder<MyOptions>()
                                       .Bind(config.GetSection("My"));
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task OptionsBindInit_MultipleInitProperties_FiresForEach()
    {
        return AnalyzerTestRunner.VerifyAsync<OptionsBindInitAnalyzer>("""
                           namespace Microsoft.Extensions.Configuration
                           {
                               public interface IConfiguration
                               {
                                   IConfigurationSection GetSection(string key);
                               }
                               public interface IConfigurationSection : IConfiguration { }
                           }

                           namespace Microsoft.Extensions.Options
                           {
                               public class OptionsBuilder<T> where T : class
                               {
                                   public OptionsBuilder<T> Bind(Microsoft.Extensions.Configuration.IConfigurationSection section) => this;
                               }
                           }

                           public class MyOptions
                           {
                               public string Name { get; {|E128033:init|}; }
                               public int Count { get; {|E128033:init|}; }
                           }

                           class Setup
                           {
                               void Configure(Microsoft.Extensions.Configuration.IConfiguration config)
                               {
                                   new Microsoft.Extensions.Options.OptionsBuilder<MyOptions>()
                                       .Bind(config.GetSection("My"));
                               }
                           }
                           """);
    }

    #endregion Fires

    #region Does Not Fire

    [Fact]
    [Trait("Category", "CI")]
    public Task OptionsBindInit_PropertyWithSet_NoFire()
    {
        return AnalyzerTestRunner.VerifyAsync<OptionsBindInitAnalyzer>("""
                           namespace Microsoft.Extensions.Configuration
                           {
                               public interface IConfiguration
                               {
                                   IConfigurationSection GetSection(string key);
                               }
                               public interface IConfigurationSection : IConfiguration { }
                           }

                           namespace Microsoft.Extensions.Options
                           {
                               public class OptionsBuilder<T> where T : class
                               {
                                   public OptionsBuilder<T> Bind(Microsoft.Extensions.Configuration.IConfigurationSection section) => this;
                               }
                           }

                           public class MyOptions
                           {
                               public string Name { get; set; } = string.Empty;
                           }

                           class Setup
                           {
                               void Configure(Microsoft.Extensions.Configuration.IConfiguration config)
                               {
                                   new Microsoft.Extensions.Options.OptionsBuilder<MyOptions>()
                                       .Bind(config.GetSection("My"));
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task OptionsBindInit_NoBindCall_NoFire()
    {
        return AnalyzerTestRunner.VerifyAsync<OptionsBindInitAnalyzer>("""
                           public class MyOptions
                           {
                               public string Name { get; init; }
                           }

                           class Consumer
                           {
                               void Use()
                               {
                                   var opts = new MyOptions { Name = "test" };
                               }
                           }
                           """);
    }

    #endregion Does Not Fire
}

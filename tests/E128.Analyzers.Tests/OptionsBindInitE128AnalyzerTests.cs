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

    [Fact]
    [Trait("Category", "CI")]
    public Task OptionsBindInit_ExtensionBind_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<OptionsBindInitAnalyzer>("""
                           using Microsoft.Extensions.DependencyInjection;

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
                                   public OptionsBuilder<T> Name(string name) => this;
                               }
                           }

                           namespace Microsoft.Extensions.DependencyInjection
                           {
                               public static class OptionsBuilderConfigurationExtensions
                               {
                                   public static Microsoft.Extensions.Options.OptionsBuilder<T> Bind<T>(
                                       this Microsoft.Extensions.Options.OptionsBuilder<T> builder,
                                       Microsoft.Extensions.Configuration.IConfigurationSection section) where T : class
                                   {
                                       return builder;
                                   }
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
    public Task OptionsBindInit_BindConfiguration_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<OptionsBindInitAnalyzer>("""
                           using Microsoft.Extensions.DependencyInjection;

                           namespace Microsoft.Extensions.Configuration
                           {
                               public interface IConfigurationSection { }
                           }

                           namespace Microsoft.Extensions.Options
                           {
                               public class OptionsBuilder<T> where T : class
                               {
                                   public OptionsBuilder<T> Name(string name) => this;
                               }
                           }

                           namespace Microsoft.Extensions.DependencyInjection
                           {
                               public static class OptionsBuilderConfigurationExtensions
                               {
                                   public static Microsoft.Extensions.Options.OptionsBuilder<T> BindConfiguration<T>(
                                       this Microsoft.Extensions.Options.OptionsBuilder<T> builder,
                                       string section) where T : class
                                   {
                                       return builder;
                                   }
                               }
                           }

                           public class MyOptions
                           {
                               public string Name { get; {|E128033:init|}; }
                           }

                           class Setup
                           {
                               void Configure()
                               {
                                   new Microsoft.Extensions.Options.OptionsBuilder<MyOptions>()
                                       .BindConfiguration("My");
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task OptionsBindInit_ConditionalAccessBind_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<OptionsBindInitAnalyzer>("""
                           using Microsoft.Extensions.DependencyInjection;

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
                               public class OptionsBuilder<T> where T : class { }
                           }

                           namespace Microsoft.Extensions.DependencyInjection
                           {
                               public static class OptionsBuilderConfigurationExtensions
                               {
                                   public static Microsoft.Extensions.Options.OptionsBuilder<T> Bind<T>(
                                       this Microsoft.Extensions.Options.OptionsBuilder<T> builder,
                                       Microsoft.Extensions.Configuration.IConfigurationSection section) where T : class
                                   {
                                       return builder;
                                   }
                               }
                           }

                           public class MyOptions
                           {
                               public string Name { get; {|E128033:init|}; }
                           }

                           class Setup
                           {
                               void Configure(
                                   Microsoft.Extensions.Configuration.IConfiguration config,
                                   Microsoft.Extensions.Options.OptionsBuilder<MyOptions> builder)
                               {
                                   builder?.Bind(config.GetSection("My"));
                               }
                           }
                           """);
    }

    #endregion Does Not Fire
}

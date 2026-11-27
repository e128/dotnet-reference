using System.Threading.Tasks;
using E128.Analyzers.Design;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class OptionsPropertyAccessibilityAnalyzerTests
{
    private const string Stubs = """
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

                public static Microsoft.Extensions.Options.OptionsBuilder<T> BindConfiguration<T>(
                    this Microsoft.Extensions.Options.OptionsBuilder<T> builder,
                    string section) where T : class
                {
                    return builder;
                }
            }

            public static class OptionsConfigurationServiceCollectionExtensions
            {
                public static void Configure<T>(object services) where T : class { }
            }
        }

        """;

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheExtensionBindBindsAnInternalProperty()
    {
        return AnalyzerTestRunner.VerifyAsync<OptionsPropertyAccessibilityAnalyzer>(Stubs + """
            class Setup
            {
                void Configure(Microsoft.Extensions.Configuration.IConfiguration config)
                {
                    new Microsoft.Extensions.Options.OptionsBuilder<MyOptions>()
                        .Bind(config.GetSection("My"));
                }
            }

            public class MyOptions
            {
                internal string {|E128257:Name|} { get; set; } = string.Empty;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheBindConfigurationExtensionBindsTheType()
    {
        return AnalyzerTestRunner.VerifyAsync<OptionsPropertyAccessibilityAnalyzer>(Stubs + """
            class Setup
            {
                void Configure()
                {
                    new Microsoft.Extensions.Options.OptionsBuilder<MyOptions>()
                        .BindConfiguration("My");
                }
            }

            public class MyOptions
            {
                internal string {|E128257:Name|} { get; set; } = string.Empty;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenThePublicPropertyHasAnInternalSetter()
    {
        return AnalyzerTestRunner.VerifyAsync<OptionsPropertyAccessibilityAnalyzer>(Stubs + """
            class Setup
            {
                void Configure(Microsoft.Extensions.Configuration.IConfiguration config)
                {
                    new Microsoft.Extensions.Options.OptionsBuilder<MyOptions>()
                        .Bind(config.GetSection("My"));
                }
            }

            public class MyOptions
            {
                public string {|E128257:Name|} { get; internal set; } = string.Empty;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenConfigureBindsTheType()
    {
        return AnalyzerTestRunner.VerifyAsync<OptionsPropertyAccessibilityAnalyzer>(Stubs + """
            class Setup
            {
                void Configure(object services)
                {
                    Microsoft.Extensions.DependencyInjection.OptionsConfigurationServiceCollectionExtensions
                        .Configure<MyOptions>(services);
                }
            }

            public class MyOptions
            {
                internal string {|E128257:Name|} { get; set; } = string.Empty;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheBoundPropertyIsPublic()
    {
        return AnalyzerTestRunner.VerifyAsync<OptionsPropertyAccessibilityAnalyzer>(Stubs + """
            class Setup
            {
                void Configure(Microsoft.Extensions.Configuration.IConfiguration config)
                {
                    new Microsoft.Extensions.Options.OptionsBuilder<MyOptions>()
                        .Bind(config.GetSection("My"));
                }
            }

            public class MyOptions
            {
                public string Name { get; set; } = string.Empty;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenThePropertyIsStatic()
    {
        return AnalyzerTestRunner.VerifyAsync<OptionsPropertyAccessibilityAnalyzer>(Stubs + """
            class Setup
            {
                void Configure(Microsoft.Extensions.Configuration.IConfiguration config)
                {
                    new Microsoft.Extensions.Options.OptionsBuilder<MyOptions>()
                        .Bind(config.GetSection("My"));
                }
            }

            public class MyOptions
            {
                internal static string Name { get; set; } = string.Empty;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheTypeIsNotBound()
    {
        return AnalyzerTestRunner.VerifyAsync<OptionsPropertyAccessibilityAnalyzer>("""
                           public class MyOptions
                           {
                               internal string Name { get; set; } = string.Empty;
                           }

                           class Consumer
                           {
                               void Use()
                               {
                                   var options = new MyOptions { Name = "test" };
                               }
                           }
                           """);
    }
}

using System.Threading.Tasks;
using E128.Analyzers.Design;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class OptionsValidationMissingAnalyzerBranchCoverageGapTests
{
    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<OptionsValidationMissingAnalyzer>(code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenNoValidatorTypeOrServiceCollectionTypeExists()
    {
        return VerifyAsync("""
            using System;
            using Microsoft.Extensions.DependencyInjection;

            namespace Microsoft.Extensions.Options
            {
                public sealed class OptionsBuilder<TOptions>
                    where TOptions : class
                {
                }
            }

            namespace Microsoft.Extensions.DependencyInjection
            {
                using Microsoft.Extensions.Options;

                public static class OptionsServiceCollectionExtensions
                {
                    public static OptionsBuilder<TOptions> AddOptions<TOptions>()
                        where TOptions : class
                    {
                        return new OptionsBuilder<TOptions>();
                    }

                    public static void Configure<TOptions>(Action<TOptions> configure)
                        where TOptions : class
                    {
                    }
                }

                public static class OptionsBuilderConfigurationExtensions
                {
                    public static OptionsBuilder<TOptions> Bind<TOptions>(this OptionsBuilder<TOptions> builder, object configuration)
                        where TOptions : class
                    {
                        return builder;
                    }
                }
            }

            class MyOptions
            {
            }

            class Startup
            {
                void Run(object configuration)
                {
                    {|E128179:OptionsServiceCollectionExtensions.AddOptions<MyOptions>()|}.Bind(configuration);
                    OptionsServiceCollectionExtensions.Configure<MyOptions>(options => { });
                    Local<int>();
                }

                void Local<T>()
                {
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAValidatorTypeExistsButNoServiceCollectionTypeDoes()
    {
        return VerifyAsync("""
            using Microsoft.Extensions.DependencyInjection;

            namespace Microsoft.Extensions.Options
            {
                public sealed class OptionsBuilder<TOptions>
                    where TOptions : class
                {
                }

                public interface IValidateOptions<TOptions>
                    where TOptions : class
                {
                }
            }

            namespace Microsoft.Extensions.DependencyInjection
            {
                using Microsoft.Extensions.Options;

                public static class OptionsServiceCollectionExtensions
                {
                    public static OptionsBuilder<TOptions> AddOptions<TOptions>()
                        where TOptions : class
                    {
                        return new OptionsBuilder<TOptions>();
                    }
                }

                public static class OptionsBuilderConfigurationExtensions
                {
                    public static OptionsBuilder<TOptions> Bind<TOptions>(this OptionsBuilder<TOptions> builder, object configuration)
                        where TOptions : class
                    {
                        return builder;
                    }
                }
            }

            class MyOptions
            {
            }

            class Startup
            {
                void Run(object configuration)
                {
                    {|E128179:OptionsServiceCollectionExtensions.AddOptions<MyOptions>()|}.Bind(configuration);
                }
            }
            """);
    }
}

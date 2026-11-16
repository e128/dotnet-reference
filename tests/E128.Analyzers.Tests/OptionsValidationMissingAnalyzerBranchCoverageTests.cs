using System.Threading.Tasks;
using E128.Analyzers.Design;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class OptionsValidationMissingAnalyzerBranchCoverageTests
{
    // A compact copy of the framework surface that the rule resolves by metadata name.
    private const string Stubs = """
        using System;
        using Microsoft.Extensions.Configuration;
        using Microsoft.Extensions.DependencyInjection;
        using Microsoft.Extensions.Options;

        namespace Microsoft.Extensions.Configuration
        {
            public interface IConfiguration
            {
            }
        }

        namespace Microsoft.Extensions.Options
        {
            public sealed class OptionsBuilder<TOptions>
                where TOptions : class
            {
                public OptionsBuilder<TOptions> Validate(Func<TOptions, bool> validate)
                {
                    return this;
                }

                public OptionsBuilder<TOptions> Tweak(object value)
                {
                    return this;
                }
            }

            public interface IValidateOptions<TOptions>
                where TOptions : class
            {
            }
        }

        namespace Microsoft.Extensions.DependencyInjection
        {
            using System;
            using Microsoft.Extensions.Configuration;
            using Microsoft.Extensions.Options;

            public interface IServiceCollection
            {
            }

            public static class ServiceCollectionServiceExtensions
            {
                public static IServiceCollection AddSingleton<TService, TImplementation>(this IServiceCollection services)
                    where TService : class
                    where TImplementation : class, TService
                {
                    return services;
                }

                public static IServiceCollection AddSingleton<TService>(this IServiceCollection services)
                    where TService : class
                {
                    return services;
                }
            }

            public static class OptionsServiceCollectionExtensions
            {
                public static OptionsBuilder<TOptions> AddOptions<TOptions>(this IServiceCollection services)
                    where TOptions : class
                {
                    return new OptionsBuilder<TOptions>();
                }

                public static OptionsBuilder<TOptions> AddOptions<TOptions>(this IServiceCollection services, string name)
                    where TOptions : class
                {
                    return new OptionsBuilder<TOptions>();
                }

                public static IServiceCollection Configure<TOptions>(this IServiceCollection services, Action<TOptions> configureOptions)
                    where TOptions : class
                {
                    return services;
                }

                public static IServiceCollection PostConfigure<TOptions>(this IServiceCollection services, Action<TOptions> configureOptions)
                    where TOptions : class
                {
                    return services;
                }

                public static OptionsBuilder<TOptions> AddOptions<TOptions>(this IServiceCollection services, string name, int extra)
                    where TOptions : class
                {
                    return new OptionsBuilder<TOptions>();
                }
            }

            public static class OptionsConfigurationServiceCollectionExtensions
            {
                public static IServiceCollection Configure<TOptions>(this IServiceCollection services, IConfiguration configuration)
                    where TOptions : class
                {
                    return services;
                }
            }

            public static class OptionsBuilderConfigurationExtensions
            {
                public static OptionsBuilder<TOptions> Bind<TOptions>(this OptionsBuilder<TOptions> builder, IConfiguration configuration)
                    where TOptions : class
                {
                    return builder;
                }
            }

            public static class OptionsBuilderExtensions
            {
                public static OptionsBuilder<TOptions> ValidateOnStart<TOptions>(this OptionsBuilder<TOptions> builder)
                    where TOptions : class
                {
                    return builder;
                }
            }
        }

        class MyOptions
        {
            public string Name { get; set; } = "";
        }

        class OtherOptions
        {
            public string Name { get; set; } = "";
        }

        class MyValidator : IValidateOptions<MyOptions>
        {
        }

        interface ISomething<T>
        {
        }

        class Something : ISomething<MyOptions>
        {
        }

        class OtherSomething : ISomething<OtherOptions>
        {
        }

        """;

    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<OptionsValidationMissingAnalyzer>(Stubs + code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheOptionsPackageTypesAreMissing()
    {
        return AnalyzerTestRunner.VerifyAsync<OptionsValidationMissingAnalyzer>("""
            class Builder
            {
                public Builder AddOptions<T>() { return this; }
                public Builder Bind(object configuration) { return this; }
            }

            class Startup
            {
                void Configure(Builder builder, object configuration)
                {
                    builder.AddOptions<Startup>().Bind(configuration);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenALateBoundCallFollowsTheRegistration()
    {
        return VerifyAsync("""
            class Startup
            {
                void Configure(IServiceCollection services, IConfiguration configuration)
                {
                    {|E128179:services.AddOptions<MyOptions>()|}.Bind(configuration).Tweak((dynamic)configuration);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForTheUnreducedStaticRegistrationCall()
    {
        return VerifyAsync("""
            class Startup
            {
                void Configure(IServiceCollection services, IConfiguration configuration)
                {
                    {|E128179:OptionsServiceCollectionExtensions.AddOptions<MyOptions>(services)|}.Bind(configuration);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAnotherMemberInTheFileRegistersTheValidator()
    {
        return VerifyAsync("""
            class Startup
            {
                void Configure(IServiceCollection services, IConfiguration configuration)
                {
                    services.Configure<MyOptions>(configuration);
                }

                void Validators(IServiceCollection services)
                {
                    Helper();
                    services.AddSingleton<Something>();
                    services.AddSingleton<ISomething<OtherOptions>, OtherSomething>();
                    services.AddSingleton<IValidateOptions<MyOptions>, MyValidator>();
                }

                void Helper()
                {
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheFileValidatorTargetsAnotherOptionsType()
    {
        return VerifyAsync("""
            class Startup
            {
                void Configure(IServiceCollection services, IConfiguration configuration)
                {
                    {|E128179:services.Configure<MyOptions>(configuration)|};
                }

                void Validators(IServiceCollection services, Startup other)
                {
                    other.Helper();
                    services.AddSingleton<IValidateOptions<OtherOptions>>();
                    services.AddSingleton<ISomething<OtherOptions>, OtherSomething>();
                }

                void Helper()
                {
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAValidationCallFollowsABindCallOnTheBuilder()
    {
        return VerifyAsync("""
            class Startup
            {
                void Configure(IServiceCollection services, IConfiguration configuration)
                {
                    services.AddOptions<MyOptions>().Bind(configuration).ValidateOnStart();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForACallToALocalMethodNamedLikeTheRegistration()
    {
        return VerifyAsync("""
            class Startup
            {
                void Configure(IServiceCollection services)
                {
                }

                void Run(IServiceCollection services)
                {
                    Configure(services);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForALateBoundRegistrationCall()
    {
        return VerifyAsync("""
            class Startup
            {
                void Run(IServiceCollection services, IConfiguration configuration)
                {
                    dynamic late = services;
                    late.AddOptions<MyOptions>().Bind(configuration);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAnAddOptionsOverloadWithMoreParameters()
    {
        return VerifyAsync("""
            class Startup
            {
                void Run(IServiceCollection services, IConfiguration configuration)
                {
                    services.AddOptions<MyOptions>("named", 1).Bind(configuration);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAPostConfigureCallFollowsTheRegistration()
    {
        return VerifyAsync("""
            class Startup
            {
                void Run(IServiceCollection services, IConfiguration configuration)
                {
                    {|E128179:services.Configure<MyOptions>(configuration)|}.PostConfigure<MyOptions>(options => { });
                }
            }
            """);
    }

}

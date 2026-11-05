using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class OptionsValidationMissingAnalyzerTests
{
    // The options packages are not part of the reference set, so the snippet declares the public
    // surface the rule matches. Each type keeps the framework namespace and member shape.
    private const string Stubs = """
                                 using Microsoft.Extensions.Configuration;
                                 using Microsoft.Extensions.DependencyInjection;

                                 namespace Microsoft.Extensions.Configuration
                                 {
                                     public interface IConfiguration
                                     {
                                     }
                                 }

                                 namespace Microsoft.Extensions.Options
                                 {
                                     using System;

                                     public sealed class OptionsBuilder<TOptions>
                                         where TOptions : class
                                     {
                                         public OptionsBuilder<TOptions> Validate(Func<TOptions, bool> validate)
                                         {
                                             return this;
                                         }
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

                                     public static class OptionsServiceCollectionExtensions
                                     {
                                         public static OptionsBuilder<TOptions> AddOptions<TOptions>(this IServiceCollection services)
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

                                         public static OptionsBuilder<TOptions> BindConfiguration<TOptions>(this OptionsBuilder<TOptions> builder, string sectionName)
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

                                         public static OptionsBuilder<TOptions> ValidateEagerly<TOptions>(this OptionsBuilder<TOptions> builder)
                                             where TOptions : class
                                         {
                                             return builder;
                                         }

                                         public static OptionsBuilder<TOptions> ValidateAllProperties<TOptions>(this OptionsBuilder<TOptions> builder)
                                             where TOptions : class
                                         {
                                             return builder;
                                         }
                                     }

                                     public static class OptionsBuilderDataAnnotationsExtensions
                                     {
                                         public static OptionsBuilder<TOptions> ValidateDataAnnotations<TOptions>(this OptionsBuilder<TOptions> builder)
                                             where TOptions : class
                                         {
                                             return builder;
                                         }
                                     }
                                 }
                                 """;

    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<OptionsValidationMissingAnalyzer, DefaultVerifier>
        {
            TestCode = Stubs + "\n" + code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    #region Fires

    [Fact]
    [Trait("Category", "CI")]
    public Task AddOptionsBind_WithNoValidation_Fires()
    {
        return VerifyAsync("""
                           class MyOptions
                           {
                               public string Name { get; set; } = "";
                           }

                           class Startup
                           {
                               void Configure(IServiceCollection services, IConfiguration configuration)
                               {
                                   {|E128179:services.AddOptions<MyOptions>()|}.Bind(configuration);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AddOptionsBindConfiguration_WithNoValidation_Fires()
    {
        return VerifyAsync("""
                           class MyOptions
                           {
                               public string Name { get; set; } = "";
                           }

                           class Startup
                           {
                               void Configure(IServiceCollection services)
                               {
                                   {|E128179:services.AddOptions<MyOptions>()|}.BindConfiguration("My");
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConfigureWithConfiguration_WithNoValidation_Fires()
    {
        return VerifyAsync("""
                           class MyOptions
                           {
                               public string Name { get; set; } = "";
                           }

                           class Startup
                           {
                               void Configure(IServiceCollection services, IConfiguration configuration)
                               {
                                   {|E128179:services.Configure<MyOptions>(configuration)|};
                               }
                           }
                           """);
    }

    #endregion Fires

    #region Silent

    [Fact]
    [Trait("Category", "CI")]
    public Task Bind_WithValidate_NoDiagnostic()
    {
        return VerifyAsync("""
                           class MyOptions
                           {
                               public string Name { get; set; } = "";
                           }

                           class Startup
                           {
                               void Configure(IServiceCollection services, IConfiguration configuration)
                               {
                                   services.AddOptions<MyOptions>()
                                       .Bind(configuration)
                                       .Validate(options => options.Name.Length > 0);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Bind_WithValidateDataAnnotationsAndValidateOnStart_NoDiagnostic()
    {
        return VerifyAsync("""
                           class MyOptions
                           {
                               public string Name { get; set; } = "";
                           }

                           class Startup
                           {
                               void Configure(IServiceCollection services, IConfiguration configuration)
                               {
                                   services.AddOptions<MyOptions>()
                                       .Bind(configuration)
                                       .ValidateDataAnnotations()
                                       .ValidateOnStart();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Bind_WithValidateEagerly_NoDiagnostic()
    {
        return VerifyAsync("""
                           class MyOptions
                           {
                               public string Name { get; set; } = "";
                           }

                           class Startup
                           {
                               void Configure(IServiceCollection services, IConfiguration configuration)
                               {
                                   services.AddOptions<MyOptions>()
                                       .Bind(configuration)
                                       .ValidateEagerly();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Bind_WithValidateAllProperties_NoDiagnostic()
    {
        return VerifyAsync("""
                           class MyOptions
                           {
                               public string Name { get; set; } = "";
                           }

                           class Startup
                           {
                               void Configure(IServiceCollection services, IConfiguration configuration)
                               {
                                   services.AddOptions<MyOptions>()
                                       .Bind(configuration)
                                       .ValidateAllProperties();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AddOptions_WithNoBindCall_NoDiagnostic()
    {
        return VerifyAsync("""
                           class MyOptions
                           {
                               public string Name { get; set; } = "";
                           }

                           class Startup
                           {
                               void Configure(IServiceCollection services)
                               {
                                   services.AddOptions<MyOptions>();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task OtherRegistrationShapes_NoDiagnostic()
    {
        return VerifyAsync("""
                           class MyOptions
                           {
                               public string Name { get; set; } = "";
                           }

                           class Startup
                           {
                               void Configure(IServiceCollection services)
                               {
                                   services.Configure<MyOptions>(options => options.Name = "x");
                                   services.PostConfigure<MyOptions>(options => options.Name = "y");
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task UserTypeWithSameMethodNames_NoDiagnostic()
    {
        return VerifyAsync("""
                           class MyOptions
                           {
                               public string Name { get; set; } = "";
                           }

                           class FakeBuilder
                           {
                               public FakeBuilder AddOptions<T>()
                               {
                                   return this;
                               }

                               public FakeBuilder Bind(IConfiguration configuration)
                               {
                                   return this;
                               }
                           }

                           class Startup
                           {
                               void Configure(IConfiguration configuration)
                               {
                                   new FakeBuilder().AddOptions<MyOptions>().Bind(configuration);
                               }
                           }
                           """);
    }

    #endregion Silent
}

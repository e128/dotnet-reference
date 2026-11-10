using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class LazyFieldThreadSafetyAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        return AnalyzerTestRunner.VerifyAsync<LazyFieldThreadSafetyAnalyzer>(code, expected);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task StaticField_None_Fires()
    {
        return VerifyAsync("""
                           using System;
                           using System.Threading;

                           class Holder
                           {
                               private static readonly Lazy<int> {|E128194:Value = new Lazy<int>(LazyThreadSafetyMode.None)|};
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task StaticField_IsThreadSafeFalse_Fires()
    {
        return VerifyAsync("""
                           using System;

                           class Holder
                           {
                               private static readonly Lazy<int> {|E128194:Value = new Lazy<int>(() => 1, false)|};
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task StaticField_IsThreadSafeTrue_DoesNotFire()
    {
        return VerifyAsync("""
                           using System;

                           class Holder
                           {
                               private static readonly Lazy<int> Value = new Lazy<int>(() => 1, true);
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task InstanceField_None_Fires()
    {
        return VerifyAsync("""
                           using System;
                           using System.Threading;

                           class Holder
                           {
                               private readonly Lazy<int> {|E128194:Value = new Lazy<int>(LazyThreadSafetyMode.None)|};
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FactoryConstructor_None_Fires()
    {
        return VerifyAsync("""
                           using System;
                           using System.Threading;

                           class Holder
                           {
                               private static readonly Lazy<int> {|E128194:Value = new Lazy<int>(() => 1, LazyThreadSafetyMode.None)|};
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NamedModeArgument_None_Fires()
    {
        return VerifyAsync("""
                           using System;
                           using System.Threading;

                           class Holder
                           {
                               private static readonly Lazy<int> {|E128194:Value = new Lazy<int>(mode: LazyThreadSafetyMode.None)|};
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ExecutionAndPublication_DoesNotFire()
    {
        return VerifyAsync("""
                           using System;
                           using System.Threading;

                           class Holder
                           {
                               private static readonly Lazy<int> Value =
                                   new Lazy<int>(LazyThreadSafetyMode.ExecutionAndPublication);
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PublicationOnly_DoesNotFire()
    {
        return VerifyAsync("""
                           using System;
                           using System.Threading;

                           class Holder
                           {
                               private static readonly Lazy<int> Value =
                                   new Lazy<int>(LazyThreadSafetyMode.PublicationOnly);
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task OmittedMode_DoesNotFire()
    {
        return VerifyAsync("""
                           using System;

                           class Holder
                           {
                               private static readonly Lazy<int> Value = new Lazy<int>(() => 1);
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PropertyInitializer_DoesNotFire()
    {
        return VerifyAsync("""
                           using System;
                           using System.Threading;

                           class Holder
                           {
                               public Lazy<int> Value { get; } =
                                   new Lazy<int>(LazyThreadSafetyMode.None);
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Local_DoesNotFire()
    {
        return VerifyAsync("""
                           using System;
                           using System.Threading;

                           class Holder
                           {
                               void Run()
                               {
                                   Lazy<int> value = new Lazy<int>(LazyThreadSafetyMode.None);
                                   _ = value;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NonConstantMode_DoesNotFire()
    {
        return VerifyAsync("""
                           using System;
                           using System.Threading;

                           class Holder
                           {
                               private static readonly Lazy<int> Value = new Lazy<int>(GetMode());

                               private static LazyThreadSafetyMode GetMode() =>
                                   LazyThreadSafetyMode.ExecutionAndPublication;
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FactoryMethodInitializer_DoesNotFire()
    {
        return VerifyAsync("""
                           using System;
                           using System.Threading;

                           class Holder
                           {
                               private static readonly Lazy<int> Value = Create();

                               private static Lazy<int> Create() =>
                                   new Lazy<int>(LazyThreadSafetyMode.None);
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ShadowedLazyType_DoesNotFire()
    {
        return VerifyAsync("""
                           namespace Custom
                           {
                               public sealed class Lazy<T>
                               {
                                   public Lazy()
                                   {
                                   }
                               }
                           }

                           class Holder
                           {
                               private readonly Custom.Lazy<int> Value = new Custom.Lazy<int>();
                           }
                           """);
    }
}

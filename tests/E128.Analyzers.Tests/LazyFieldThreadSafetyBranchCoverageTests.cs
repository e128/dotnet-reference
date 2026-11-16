using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

/// <summary>
///     Covers the field and constructor shapes the Lazy thread safety rule reads.
/// </summary>
public sealed class LazyFieldThreadSafetyBranchCoverageTests
{
    private static Task VerifyAsync(string source, AnalyzerTestOptions? options = null)
    {
        return AnalyzerTestRunner.VerifyAsync<LazyFieldThreadSafetyAnalyzer>(
            "using System;\nusing System.Threading;\n" + source,
            expected: null,
            options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheModeArgumentComesBeforeTheFactoryByName()
    {
        return VerifyAsync("""
                           class C
                           {
                               private static readonly Lazy<int> {|E128194:Value = new Lazy<int>(mode: LazyThreadSafetyMode.None, valueFactory: () => 1)|};
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForEachVariableOfAMultiFieldDeclaration()
    {
        return VerifyAsync("""
                           class C
                           {
                               private static readonly Lazy<int> Plain, {|E128194:Unsafe = new Lazy<int>(() => 1, LazyThreadSafetyMode.None)|}, Safe = new Lazy<int>(() => 2);
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheThreadSafetyFlagIsNotAConstant()
    {
        return VerifyAsync("""
                           class C
                           {
                               private static bool flag = true;

                               private static readonly Lazy<int> Value = new Lazy<int>(() => 1, flag);
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheInitializerHasNoArgumentList()
    {
        return VerifyAsync("""
                           class C
                           {
                               private static readonly Lazy<int> Value = new Lazy<int> { };
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCreatedTypeDerivesFromLazy()
    {
        return VerifyAsync("""
                           class DerivedLazy : Lazy<int>
                           {
                               public DerivedLazy(LazyThreadSafetyMode mode)
                                   : base(() => 1, mode)
                               {
                               }
                           }

                           class C
                           {
                               private static readonly Lazy<int> Value = new DerivedLazy(LazyThreadSafetyMode.None);
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheFieldTypeIsNotLazy()
    {
        return VerifyAsync("""
                           class C
                           {
                               private static readonly object Boxed = new Lazy<int>(() => 1, LazyThreadSafetyMode.None);
                               private static readonly dynamic Late = new Lazy<int>(() => 1, LazyThreadSafetyMode.None);
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheFieldHasNoInitializerOrACreationThatDoesNotBind()
    {
        return VerifyAsync(
            """
            class C
            {
                private static Lazy<int> Empty;

                private static readonly Lazy<int> Broken = new Lazy<int>(missing, LazyThreadSafetyMode.None);
            }
            """,
            new AnalyzerTestOptions { CompilerDiagnostics = CompilerDiagnostics.None });
    }
}

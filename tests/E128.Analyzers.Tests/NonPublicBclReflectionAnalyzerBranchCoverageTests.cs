using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class NonPublicBclReflectionAnalyzerBranchCoverageTests
{
    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<NonPublicBclReflectionAnalyzer>(
            "using System; using System.Reflection;\n" + code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForPropertyAndConstructorLookups()
    {
        return VerifyAsync("""
            class C
            {
                void M()
                {
                    var a = {|E128137:typeof(string).GetProperty("Length", BindingFlags.NonPublic | BindingFlags.Instance)|};
                    var b = {|E128137:typeof(string).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAStaticImportOfTheNonPublicFlag()
    {
        return AnalyzerTestRunner.VerifyAsync<NonPublicBclReflectionAnalyzer>("""
            using System;
            using System.Reflection;
            using static System.Reflection.BindingFlags;

            class C
            {
                void M()
                {
                    var method = {|E128137:typeof(string).GetMethod("Empty", NonPublic | Static)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenANonPublicNameDoesNotResolveToBindingFlags()
    {
        return VerifyAsync("""
            class C
            {
                const BindingFlags NonPublic = BindingFlags.Public;

                void M()
                {
                    var method = typeof(string).GetMethod("Empty", NonPublic | BindingFlags.Static);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForOtherMethodsOnTypeAndOtherReceivers()
    {
        return VerifyAsync("""
            class Other
            {
                public object GetMethod(string name, BindingFlags flags) => null;
            }

            class C
            {
                void M(Other other, Type type)
                {
                    var a = other.GetMethod("M", BindingFlags.NonPublic);
                    var b = type.GetMethod("M", BindingFlags.NonPublic);
                    var c = typeof(string).GetMethods(BindingFlags.Public);
                }
            }
            """);
    }
}

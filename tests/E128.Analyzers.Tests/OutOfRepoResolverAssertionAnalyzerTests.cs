using System.Threading.Tasks;
using E128.Analyzers.Testing;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class OutOfRepoResolverAssertionAnalyzerTests
{
    private static readonly ReferenceAssemblies Net100WithXunit = ReferenceAssemblies.Net.Net100
        .AddPackages([
            new PackageIdentity("xunit.v3.core", "3.2.2"),
            new PackageIdentity("xunit.v3.assert", "3.2.2")
        ]);

    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        return VerifyWithResolversAsync("CssPathResolver.Resolve", code, expected);
    }

    private static Task VerifySilentlyAsync(string code)
    {
        var test = new CSharpAnalyzerTest<OutOfRepoResolverAssertionAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = Net100WithXunit
        };
        return test.RunAsync();
    }

    private static Task VerifyWithResolversAsync(string resolvers, string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<OutOfRepoResolverAssertionAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = Net100WithXunit
        };
        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", "is_global = true\ne128_out_of_repo_resolvers = " + resolvers));
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task OutOfRepoResolverAssertionAnalyzer_ReportsTest_WhenSkipGuardIsAbsent()
    {
        return VerifyAsync("""
                           using Xunit;

                           public static class CssPathResolver
                           {
                               public static string Resolve(string name) => name;
                           }

                           public sealed class Subject
                           {
                               [Fact]
                               public void {|E128105:ResolvePath_ReturnsThePath|}()
                               {
                                   var path = CssPathResolver.Resolve("alpha");
                                   Assert.NotNull(path);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task OutOfRepoResolverAssertionAnalyzer_ReportsNothing_WhenSkipGuardIsPresent()
    {
        return VerifyAsync("""
                           using Xunit;

                           public static class CssPathResolver
                           {
                               public static string Resolve(string name) => name;
                           }

                           public sealed class Subject
                           {
                               [Fact]
                               public void ResolvePath_ReturnsThePath()
                               {
                                   Assert.SkipWhen(true, "the stylesheet lives outside the repository");
                                   var path = CssPathResolver.Resolve("alpha");
                                   Assert.NotNull(path);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task OutOfRepoResolverAssertionAnalyzer_ReportsTest_WhenTheSkipGuardFollowsTheResolver()
    {
        return VerifyAsync("""
                           using Xunit;

                           public static class CssPathResolver
                           {
                               public static string Resolve(string name) => name;
                           }

                           public sealed class Subject
                           {
                               [Fact]
                               public void {|E128105:ResolvePath_ReturnsThePath|}()
                               {
                                   var path = CssPathResolver.Resolve("alpha");
                                   Assert.SkipWhen(path.Length == 0, "the stylesheet lives outside the repository");
                                   Assert.NotNull(path);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task OutOfRepoResolverAssertionAnalyzer_ReportsNothing_WhenTheOptionKeyIsAbsent()
    {
        return VerifySilentlyAsync("""
                                   using Xunit;

                                   public static class CssPathResolver
                                   {
                                       public static string Resolve(string name) => name;
                                   }

                                   public sealed class Subject
                                   {
                                       [Fact]
                                       public void ResolvePath_ReturnsThePath()
                                       {
                                           var path = CssPathResolver.Resolve("alpha");
                                           Assert.NotNull(path);
                                       }
                                   }
                                   """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task OutOfRepoResolverAssertionAnalyzer_ReportsNothing_WhenTheMethodIsNotATest()
    {
        return VerifyAsync("""
                           public static class CssPathResolver
                           {
                               public static string Resolve(string name) => name;
                           }

                           public sealed class Subject
                           {
                               public void ResolvePath_ReturnsThePath()
                               {
                                   _ = CssPathResolver.Resolve("alpha");
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task OutOfRepoResolverAssertionAnalyzer_ReportsNothing_WhenTheMemberIsNotAllowlisted()
    {
        return VerifyAsync("""
                           using Xunit;

                           public static class CssPathResolver
                           {
                               public static string Resolve(string name) => name;

                               public static string ResolveFilter(string name) => name;
                           }

                           public sealed class Subject
                           {
                               [Fact]
                               public void ResolveFilter_ReturnsTheFilter()
                               {
                                   var filter = CssPathResolver.ResolveFilter("alpha");
                                   Assert.NotNull(filter);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task OutOfRepoResolverAssertionAnalyzer_ReportsNothing_WhenTheFactDeclaresSkip()
    {
        return VerifyAsync("""
                           using Xunit;

                           public static class CssPathResolver
                           {
                               public static string Resolve(string name) => name;
                           }

                           public sealed class Subject
                           {
                               [Fact(Skip = "the stylesheet lives outside the repository")]
                               public void ResolvePath_ReturnsThePath()
                               {
                                   var path = CssPathResolver.Resolve("alpha");
                                   Assert.NotNull(path);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task OutOfRepoResolverAssertionAnalyzer_ReportsNothing_WhenTheFactDeclaresSkipWhen()
    {
        return VerifyAsync("""
                           using Xunit;

                           public static class CssPathResolver
                           {
                               public static string Resolve(string name) => name;
                           }

                           public sealed class Subject
                           {
                               [Fact(SkipWhen = "CssPathResolverIsAbsent")]
                               public void ResolvePath_ReturnsThePath()
                               {
                                   var path = CssPathResolver.Resolve("alpha");
                                   Assert.NotNull(path);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task OutOfRepoResolverAssertionAnalyzer_ReportsTest_WhenABareTypeEntryMatchesEveryMember()
    {
        return VerifyWithResolversAsync("CssPathResolver", """
                                                          using Xunit;

                                                          public static class CssPathResolver
                                                          {
                                                              public static string Resolve(string name) => name;

                                                              public static string ResolveFilter(string name) => name;
                                                          }

                                                          public sealed class Subject
                                                          {
                                                              [Theory]
                                                              public void {|E128105:ResolveFilter_ReturnsTheFilter|}()
                                                              {
                                                                  var filter = CssPathResolver.ResolveFilter("alpha");
                                                                  Assert.NotNull(filter);
                                                              }
                                                          }
                                                          """);
    }
}

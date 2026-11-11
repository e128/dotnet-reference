using System.Threading.Tasks;
using E128.Analyzers.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class TestCodeCommentAnalyzerTests
{

    private static AnalyzerTestOptions Options => new() { References = Net100WithXunit };

    private static readonly ReferenceAssemblies Net100WithXunit = ReferenceAssemblies.Net.Net100
        .AddPackages([new PackageIdentity("xunit.v3.core", "3.2.2")]);

    [Fact]
    [Trait("Category", "CI")]
    public Task Analyzer_Reports_WhenFactBodyHasLineComment()
    {
        return AnalyzerTestRunner.VerifyAsync<TestCodeCommentAnalyzer>("""
                            using Xunit;

                            public class C
                            {
                                [Fact]
                                public void M()
                                {
                                    {|E128097:// comment|}
                                    var x = 1;
                                }
                            }
                            """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Analyzer_Reports_WhenTestClassHelperHasDocComment()
    {
        return AnalyzerTestRunner.VerifyAsync<TestCodeCommentAnalyzer>("""
                            using Xunit;

                            public class C
                            {
                                {|E128097:/// <summary>Helper</summary>|}
                                private static void Helper()
                                {
                                }

                                [Fact]
                                public void M()
                                {
                                }
                            }
                            """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Analyzer_ReportsNothing_WhenTypeHasNoXunitAttribute()
    {
        return AnalyzerTestRunner.VerifyAsync<TestCodeCommentAnalyzer>("""
                            public class D
                            {
                                public void M()
                                {
                                    // comment
                                    var x = 1;
                                }
                            }
                            """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Analyzer_ReportsNothing_WhenTheCommentSitsOnTheDeclaration()
    {
        // The comment is leading trivia of the class itself. It sits outside the type body, and the
        // code fix would delete a license header or a type doc comment.
        return AnalyzerTestRunner.VerifyAsync<TestCodeCommentAnalyzer>("""
                            using Xunit;

                            /// <summary>Covers the parser.</summary>
                            public class C
                            {
                                [Fact]
                                public void M()
                                {
                                }
                            }
                            """, options: Options);
    }
}

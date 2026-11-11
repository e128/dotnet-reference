using System.Threading.Tasks;
using E128.Analyzers.Style;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class GuidNewGuidTempPathE128AnalyzerTests
{
    private static DiagnosticResult Expect(int line, int column)
    {
        return new DiagnosticResult("E128025", DiagnosticSeverity.Warning)
            .WithLocation(line, column);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task GuidInPathCombine_Fires()
    {
        const string code = """
                            using System;
                            using System.IO;

                            class Foo
                            {
                                void Create()
                                {
                                    var dir = Path.Combine(Path.GetTempPath(), $"prefix_{Guid.NewGuid():N}");
                                }
                            }
                            """;
        return AnalyzerTestRunner.VerifyAsync<GuidNewGuidTempPathE128Analyzer>(code, Expect(8, 62));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task GuidNotInPathContext_NoFire()
    {
        return AnalyzerTestRunner.VerifyAsync<GuidNewGuidTempPathE128Analyzer>("""
                           using System;

                           class Foo
                           {
                               void Create()
                               {
                                   var id = Guid.NewGuid().ToString();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task GuidWithExtension_Fires()
    {
        const string code = """
                            using System;
                            using System.IO;

                            class Foo
                            {
                                void Create()
                                {
                                    var path = Path.Combine(Path.GetTempPath(), $"audio_{Guid.NewGuid():N}.wav");
                                }
                            }
                            """;
        return AnalyzerTestRunner.VerifyAsync<GuidNewGuidTempPathE128Analyzer>(code, Expect(8, 62));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task GuidInNonPathInterpolation_NoFire()
    {
        return AnalyzerTestRunner.VerifyAsync<GuidNewGuidTempPathE128Analyzer>("""
                           using System;

                           class Foo
                           {
                               void Create()
                               {
                                   var msg = $"Created item {Guid.NewGuid()} successfully";
                               }
                           }
                           """);
    }
}

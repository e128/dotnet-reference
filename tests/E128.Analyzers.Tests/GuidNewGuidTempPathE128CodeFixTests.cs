using System.Threading.Tasks;
using E128.Analyzers.Style;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class GuidNewGuidTempPathE128CodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode, DiagnosticResult expected)
    {
        var test = new CSharpCodeFixTest<GuidNewGuidTempPathE128Analyzer, GuidNewGuidTempPathCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            NumberOfFixAllIterations = 1
        };
        test.ExpectedDiagnostics.Add(expected);
        return test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task GuidInTempPathWithFormat_CodeFix_ReplacesWithGetRandomFileName()
    {
        const string source = """
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

        const string fixedCode = """
                                 using System;
                                 using System.IO;

                                 class Foo
                                 {
                                     void Create()
                                     {
                                         var dir = Path.Combine(Path.GetTempPath(), $"prefix_{Path.GetRandomFileName():N}");
                                     }
                                 }
                                 """;

        return VerifyFixAsync(source, fixedCode, new DiagnosticResult("E128025", DiagnosticSeverity.Warning).WithSpan(8, 62, 8, 76));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task GuidInTempPathWithoutFormat_CodeFix_ReplacesWithGetRandomFileName()
    {
        const string source = """
                              using System;
                              using System.IO;

                              class Foo
                              {
                                  void Create()
                                  {
                                      var dir = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.tmp");
                                  }
                              }
                              """;

        const string fixedCode = """
                                 using System;
                                 using System.IO;

                                 class Foo
                                 {
                                     void Create()
                                     {
                                         var dir = Path.Combine(Path.GetTempPath(), $"{Path.GetRandomFileName()}.tmp");
                                     }
                                 }
                                 """;

        return VerifyFixAsync(source, fixedCode, new DiagnosticResult("E128025", DiagnosticSeverity.Warning).WithSpan(8, 55, 8, 69));
    }
}

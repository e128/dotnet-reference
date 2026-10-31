using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class StringBuilderPoolCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<StringBuilderPoolAnalyzer, StringBuilderPoolCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            NumberOfFixAllIterations = 1
        }.RunAsync();
    }

    private static Task VerifyNoFixAsync(string source, params DiagnosticResult[] expected)
    {
        var test = new CSharpCodeFixTest<StringBuilderPoolAnalyzer, StringBuilderPoolCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            NumberOfFixAllInDocumentIterations = 0
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_ReplacesNewStringBuilder_WithPoolRent()
    {
        const string source = """
                              using System.Text;
                              namespace Pug.Core.Text
                              {
                                  public abstract class StringBuilderPool
                                  {
                                      public static StringBuilderPool Shared => null;
                                      public abstract StringBuilder Rent();
                                      public abstract StringBuilder Rent(int capacity);
                                      public abstract void Return(StringBuilder builder);
                                  }
                              }
                              class C
                              {
                                  void M()
                                  {
                                      var sb = {|E128081:new StringBuilder()|};
                                      sb.Append("hello");
                                      _ = sb.ToString();
                                  }
                              }
                              """;

        const string fixedCode = """
                                 using Pug.Core.Text;
                                 using System.Text;
                                 namespace Pug.Core.Text
                                 {
                                     public abstract class StringBuilderPool
                                     {
                                         public static StringBuilderPool Shared => null;
                                         public abstract StringBuilder Rent();
                                         public abstract StringBuilder Rent(int capacity);
                                         public abstract void Return(StringBuilder builder);
                                     }
                                 }
                                 class C
                                 {
                                     void M()
                                     {
                                         var sb = StringBuilderPool.Shared.Rent();
                                         sb.Append("hello");
                                         _ = sb.ToString();
                                     }
                                 }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_DropsCapacityArg_WhenReplacingWithPoolRent()
    {
        const string source = """
                              using System.Text;
                              namespace Pug.Core.Text
                              {
                                  public abstract class StringBuilderPool
                                  {
                                      public static StringBuilderPool Shared => null;
                                      public abstract StringBuilder Rent();
                                      public abstract StringBuilder Rent(int capacity);
                                      public abstract void Return(StringBuilder builder);
                                  }
                              }
                              class C
                              {
                                  void M()
                                  {
                                      var sb = {|E128081:new StringBuilder(256)|};
                                      sb.Append("hello");
                                      _ = sb.ToString();
                                  }
                              }
                              """;

        const string fixedCode = """
                                 using Pug.Core.Text;
                                 using System.Text;
                                 namespace Pug.Core.Text
                                 {
                                     public abstract class StringBuilderPool
                                     {
                                         public static StringBuilderPool Shared => null;
                                         public abstract StringBuilder Rent();
                                         public abstract StringBuilder Rent(int capacity);
                                         public abstract void Return(StringBuilder builder);
                                     }
                                 }
                                 class C
                                 {
                                     void M()
                                     {
                                         var sb = StringBuilderPool.Shared.Rent();
                                         sb.Append("hello");
                                         _ = sb.ToString();
                                     }
                                 }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoDiagnostic_WhenStringBuilderPoolNotInCompilation()
    {
        // The pool type is absent, so the analyzer must not report at all. A report without a
        // fix leaves the user with a suggestion that this compilation cannot act on.
        const string source = """
                              using System.Text;
                              class C
                              {
                                  void M()
                                  {
                                      var sb = new StringBuilder();
                                      sb.Append("hello");
                                      _ = sb.ToString();
                                  }
                              }
                              """;

        return VerifyNoFixAsync(source);
    }
}

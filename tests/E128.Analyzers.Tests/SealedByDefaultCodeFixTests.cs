using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SealedByDefaultCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<SealedByDefaultAnalyzer, SealedByDefaultCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            NumberOfFixAllIterations = 1,
            // CompilationEnd diagnostics are non-local by design.
            CodeFixTestBehaviors = CodeFixTestBehaviors.SkipLocalDiagnosticCheck
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ClassWithBase_FixAddsSealedModifier()
    {
        const string source = """
                              class Base { }
                              class {|E128005:Derived|} : Base { }
                              """;

        const string fixedCode = """
                                 class Base { }
                                 sealed class Derived : Base { }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PublicClassWithBase_FixAddsSealedAfterAccess()
    {
        const string source = """
                              public class Base { }
                              public class {|E128005:Derived|} : Base { }
                              """;

        const string fixedCode = """
                                 public class Base { }
                                 public sealed class Derived : Base { }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task MultipleUnsealedClasses_FixAllAddsSealedToAll()
    {
        const string source = """
                              class Base { }
                              class {|E128005:A|} : Base { }
                              class {|E128005:B|} : Base { }
                              """;

        const string fixedCode = """
                                 class Base { }
                                 sealed class A : Base { }
                                 sealed class B : Base { }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }
}

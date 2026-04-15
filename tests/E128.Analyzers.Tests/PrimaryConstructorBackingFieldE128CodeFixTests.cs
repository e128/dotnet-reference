using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class PrimaryConstructorBackingFieldE128CodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<PrimaryConstructorBackingFieldAnalyzer, PrimaryConstructorBackingFieldCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            NumberOfFixAllIterations = 1,
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task BackingField_Removed_ReferencesReplacedWithParameter()
    {
        return VerifyFixAsync(
            """
            class C(int value)
            {
                private readonly int {|E128017:_value|} = value;
                int Get() => _value;
            }
            """,
            """
            class C(int value)
            {
                int Get() => value;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task BackingField_MultipleReferences_AllReplaced()
    {
        return VerifyFixAsync(
            """
            class C(string name)
            {
                private readonly string {|E128017:_name|} = name;
                string GetName() => _name;
                bool IsEmpty() => _name.Length == 0;
            }
            """,
            """
            class C(string name)
            {
                string GetName() => name;
                bool IsEmpty() => name.Length == 0;
            }
            """);
    }
}

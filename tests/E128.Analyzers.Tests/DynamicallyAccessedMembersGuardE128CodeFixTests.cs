using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class DynamicallyAccessedMembersGuardE128CodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<DynamicallyAccessedMembersGuardAnalyzer, DynamicallyAccessedMembersGuardCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task RemovesDynamicallyAccessedMembersAttribute()
    {
        const string source = """
            using System.Diagnostics.CodeAnalysis;

            class C
            {
                void M([{|E128049:DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)|}] System.Type type) { }
            }
            """;

        const string fixedCode = """
            using System.Diagnostics.CodeAnalysis;

            class C
            {
                void M(System.Type type) { }
            }
            """;

        return VerifyFixAsync(source, fixedCode);
    }
}

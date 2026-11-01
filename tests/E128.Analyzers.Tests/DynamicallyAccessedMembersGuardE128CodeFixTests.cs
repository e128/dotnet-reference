using System.Threading.Tasks;
using E128.Analyzers.Design;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class DynamicallyAccessedMembersGuardE128CodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode) =>
        CodeFixTestRunner.RunAsync<DynamicallyAccessedMembersGuardAnalyzer, DynamicallyAccessedMembersGuardCodeFixProvider>(source, fixedCode);

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

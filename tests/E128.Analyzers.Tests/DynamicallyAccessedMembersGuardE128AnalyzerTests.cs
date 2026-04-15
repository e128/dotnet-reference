using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class DynamicallyAccessedMembersGuardE128AnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<DynamicallyAccessedMembersGuardAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DynamicallyAccessedMembersAttribute_Fires()
    {
        return VerifyAsync("""
            using System.Diagnostics.CodeAnalysis;

            class C
            {
                void M([{|E128049:DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)|}] System.Type type) { }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DynamicallyAccessedMembersAttribute_FullName_Fires()
    {
        return VerifyAsync("""
            using System.Diagnostics.CodeAnalysis;

            class C
            {
                void M([{|E128049:DynamicallyAccessedMembersAttribute(DynamicallyAccessedMemberTypes.All)|}] System.Type type) { }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task OtherAttribute_NoDiagnostic()
    {
        return VerifyAsync("""
            using System;

            class C
            {
                [Obsolete("test")]
                void M() { }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoAttributes_NoDiagnostic()
    {
        return VerifyAsync("""
            class C
            {
                void M() { }
            }
            """);
    }
}

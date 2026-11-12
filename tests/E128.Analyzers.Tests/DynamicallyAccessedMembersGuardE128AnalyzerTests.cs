using System.Threading.Tasks;
using E128.Analyzers.Design;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class DynamicallyAccessedMembersGuardE128AnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task DynamicallyAccessedMembersAttribute_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<DynamicallyAccessedMembersGuardAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<DynamicallyAccessedMembersGuardAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<DynamicallyAccessedMembersGuardAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<DynamicallyAccessedMembersGuardAnalyzer>("""
                           class C
                           {
                               void M() { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DynamicallyAccessedMembersAttribute_PartialPropertyWithoutImplementation_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<DynamicallyAccessedMembersGuardAnalyzer>("""
                           using System.Diagnostics.CodeAnalysis;

                           partial class C
                           {
                               [{|E128049:DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)|}]
                               private static partial System.Type {|CS9248:P|} { get; }
                           }
                           """);
    }
}

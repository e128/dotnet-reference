using System.Threading.Tasks;
using E128.Analyzers.Maintainability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class TestMethodReflectionAnalyzerBranchCoverageTests
{
    private const string Stubs = """
        using System;
        using System.Reflection;

        namespace Xunit
        {
            sealed class FactAttribute : Attribute { }
            sealed class TheoryAttribute : Attribute { }
        }

        class FactAttribute : Attribute { }
        class TheoryAttribute : Attribute { }
        class OtherAttribute : Attribute { }

        """;

    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<TestMethodReflectionAnalyzer>(Stubs + code);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("Fact")]
    [InlineData("Theory")]
    [InlineData("Xunit.Fact")]
    [InlineData("Xunit.Theory")]
    public Task Reports_ForEachRecognizedTestAttribute(string attribute)
    {
        return VerifyAsync(
            "class C { [" + attribute + "] void M() { var x = {|E128090:typeof(C).GetMethod(\"M\")|}; } }");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAReflectionMethodGroup()
    {
        return VerifyAsync("""
            class C
            {
                [Fact]
                void M(Type type)
                {
                    Func<string, MethodInfo> find = {|E128090:type.GetMethod|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForUnrelatedAttributesAndMethods()
    {
        return VerifyAsync("""
            class C
            {
                [Other]
                void M(Type type)
                {
                    var x = type.GetMethod("M");
                    var flags = BindingFlags.NonPublic;
                }

                [Fact]
                void N(Type type)
                {
                    var x = type.GetMethods();
                    var y = type.Name;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAnUnboundOrUserDefinedGetMethod()
    {
        return VerifyAsync("""
            class OtherType
            {
                public int GetMethod(string name) => 0;
            }

            class C
            {
                [Fact]
                void M(OtherType other)
                {
                    var x = other.GetMethod("M");
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForATypeNameInADeclarationAndCast()
    {
        return VerifyAsync("""
            class C
            {
                [Fact]
                void M(object value)
                {
                    {|E128090:BindingFlags|} flags = ({|E128090:BindingFlags|})value;
                }
            }
            """);
    }
}

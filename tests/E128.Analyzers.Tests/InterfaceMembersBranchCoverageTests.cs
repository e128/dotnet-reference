using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class InterfaceMembersBranchCoverageTests
{
    private const string Source = """
        using System;
        using System.Collections.Generic;

        interface IWork
        {
            void Run();
            void Run(int count);
            int Size { get; }
        }

        class Implicit : IWork
        {
            public void Run() { }
            public void Run(int count) { }
            public int Size => 1;
            public void Other() { }
            public void Run(string text) { }
        }

        class Explicit : IWork
        {
            void IWork.Run() { }
            void IWork.Run(int count) { }
            int IWork.Size => 1;
        }

        class Plain
        {
            public void Run() { }
        }

        class Base : IWork
        {
            public virtual void Run() { }
            public void Run(int count) { }
            public int Size => 1;
        }

        class Hider : Base
        {
            public new void Run() { }
        }

        struct Value : IWork
        {
            public void Run() { }
            public void Run(int count) { }
            public int Size => 1;
        }

        delegate void Handler();

        class Generic<T> : IWork
        {
            public void Run() { }
            public void Run(int count) { }
            public int Size => 1;
            public void Add(T item) { }
        }

        class Closed : Generic<int>
        {
        }
        """;

    private static (Compilation Compilation, INamedTypeSymbol Type) Get(string typeName)
    {
        var tree = CSharpSyntaxTree.ParseText(Source, cancellationToken: TestContext.Current.CancellationToken);
        var compilation = CSharpCompilation.Create(
            "InterfaceProbe",
            [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        return (compilation, compilation.GetTypeByMetadataName(typeName)!);
    }

    private static IMethodSymbol Method(INamedTypeSymbol type, string name, int parameterCount, SpecialType? parameterType = null)
    {
        return type.GetMembers(name).OfType<IMethodSymbol>().Single(
            m => m.Parameters.Length == parameterCount
                 && (parameterType is null || (m.Parameters.Length > 0 && m.Parameters[0].Type.SpecialType == parameterType)));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void Implements_IsTrueForImplicitImplementations()
    {
        var (_, type) = Get("Implicit");
        Assert.True(InterfaceMembers.Implements(Method(type, "Run", 0)));
        Assert.True(InterfaceMembers.Implements(Method(type, "Run", 1, SpecialType.System_Int32)));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void Implements_IsFalseForMethodsThatImplementNothing()
    {
        var (_, type) = Get("Implicit");
        Assert.False(InterfaceMembers.Implements(Method(type, "Other", 0)));
        Assert.False(InterfaceMembers.Implements(Method(type, "Run", 1, SpecialType.System_String)));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void Implements_IsTrueForExplicitImplementations()
    {
        var (_, type) = Get("Explicit");
        var methods = type.GetMembers().OfType<IMethodSymbol>().Where(static m => m.MethodKind is MethodKind.ExplicitInterfaceImplementation);
        Assert.All(methods, static method => Assert.True(InterfaceMembers.Implements(method)));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void Implements_IsFalseForAClassWithoutInterfaces()
    {
        var (_, type) = Get("Plain");
        Assert.False(InterfaceMembers.Implements(Method(type, "Run", 0)));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void Implements_IsFalseForInterfaceAndDelegateMembers()
    {
        var (_, iface) = Get("IWork");
        var (_, handler) = Get("Handler");
        Assert.False(InterfaceMembers.Implements(Method(iface, "Run", 0)));
        Assert.False(InterfaceMembers.Implements(handler.GetMembers("Invoke").OfType<IMethodSymbol>().Single()));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void Implements_IsFalseForAMethodThatHidesTheImplementation()
    {
        var (_, hider) = Get("Hider");
        var (_, baseType) = Get("Base");
        Assert.False(InterfaceMembers.Implements(Method(hider, "Run", 0)));
        Assert.True(InterfaceMembers.Implements(Method(baseType, "Run", 0)));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void Implements_IsTrueForStructsAndConstructedGenericTypes()
    {
        var (_, value) = Get("Value");
        var (_, closed) = Get("Closed");
        var constructed = closed.BaseType!;
        Assert.True(InterfaceMembers.Implements(Method(value, "Run", 0)));
        Assert.True(InterfaceMembers.Implements(Method(constructed, "Run", 0)));
        Assert.False(InterfaceMembers.Implements(Method(constructed, "Add", 1)));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void ImplementsMember_ReportsMethodsThatMapToAnInterfaceMember()
    {
        var (_, type) = Get("Implicit");
        Assert.True(InterfaceMembers.ImplementsMember(Method(type, "Run", 0)));
        Assert.False(InterfaceMembers.ImplementsMember(Method(type, "Other", 0)));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void ImplementsMember_IsFalseForClassesWithoutInterfaces()
    {
        var (_, type) = Get("Plain");
        Assert.False(InterfaceMembers.ImplementsMember(Method(type, "Run", 0)));
    }
}

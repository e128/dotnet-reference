using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ActivatorInLoopAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task CreateInstanceInForeach_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<ActivatorInLoopAnalyzer>("""
            using System;
            using System.Collections.Generic;
            class C
            {
                void M(IReadOnlyList<Type> types)
                {
                    foreach (Type type in types)
                    {
                        {|E128213:Activator.CreateInstance(type)|};
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task GenericCreateInstanceInLoop_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<ActivatorInLoopAnalyzer>("""
            using System;
            using System.Collections.Generic;
            class C
            {
                void M(IReadOnlyList<string> items)
                {
                    foreach (string item in items)
                    {
                        _ = item;
                        {|E128213:Activator.CreateInstance<object>()|};
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task OutsideLoop_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<ActivatorInLoopAnalyzer>("""
            using System;
            class C
            {
                object M(Type type)
                {
                    return Activator.CreateInstance(type);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConstantBoundLoop_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<ActivatorInLoopAnalyzer>("""
            using System;
            class C
            {
                void M()
                {
                    for (int i = 0; i < 3; i++)
                    {
                        Activator.CreateInstance<object>();
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CustomActivatorInLoop_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<ActivatorInLoopAnalyzer>("""
            using System.Collections.Generic;
            class Activator
            {
                public static object CreateInstance(object value) => value;
            }

            class C
            {
                void M(IReadOnlyList<object> values)
                {
                    foreach (object value in values)
                    {
                        Activator.CreateInstance(value);
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DeconstructionForeach_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<ActivatorInLoopAnalyzer>("""
            using System;
            using System.Collections.Generic;
            class C
            {
                void M(IReadOnlyList<(string Key, Type Value)> pairs)
                {
                    foreach (var (key, value) in pairs)
                    {
                        _ = key;
                        {|E128213:Activator.CreateInstance(value)|};
                    }
                }
            }
            """);
    }
}

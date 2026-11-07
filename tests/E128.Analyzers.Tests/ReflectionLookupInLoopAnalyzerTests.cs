using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ReflectionLookupInLoopAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task TypeofLookup_InForeach_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<ReflectionLookupInLoopAnalyzer>("""
            using System;
            using System.Collections.Generic;
            class C
            {
                void M(IReadOnlyList<object> items)
                {
                    foreach (object item in items)
                    {
                        {|E128207:typeof(C).GetProperty("Length")|};
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TypeLocalDeclaredOutsideLoop_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<ReflectionLookupInLoopAnalyzer>("""
            using System;
            using System.Collections.Generic;
            class C
            {
                void M(IReadOnlyList<object> items)
                {
                    Type type = typeof(C);
                    foreach (object item in items)
                    {
                        {|E128207:type.GetMethod("M")|};
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LookupOutsideLoop_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<ReflectionLookupInLoopAnalyzer>("""
            using System;
            class C
            {
                void M()
                {
                    typeof(C).GetProperty("Length");
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ReceiverUsesLoopVariable_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<ReflectionLookupInLoopAnalyzer>("""
            using System.Collections.Generic;
            class C
            {
                void M(IReadOnlyList<object> items)
                {
                    foreach (object item in items)
                    {
                        item.GetType().GetProperty("Length");
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NonTypeReceiver_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<ReflectionLookupInLoopAnalyzer>("""
            using System.Collections.Generic;
            class Widget
            {
                public object GetProperty(string name) => name;
            }

            class C
            {
                void M(IReadOnlyList<object> items, Widget widget)
                {
                    foreach (object item in items)
                    {
                        widget.GetProperty("Length");
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task WhileLoopCallKey_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<ReflectionLookupInLoopAnalyzer>("""
            class Reader
            {
                public bool Read() => false;
                public string GetString(int index) => string.Empty;
            }

            class C
            {
                void M(Reader reader)
                {
                    while (reader.Read())
                    {
                        typeof(C).GetProperty(reader.GetString(0));
                    }
                }
            }
            """);
    }
}

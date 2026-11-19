using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class DeadFieldStoreAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenASecondAssignmentOverwritesTheFirst()
    {
        return AnalyzerTestRunner.VerifyAsync<DeadFieldStoreAnalyzer>("""
            class C
            {
                private int _value;

                void M()
                {
                    {|E128250:_value = 1|};
                    _value = 2;
                    Use(_value);
                }

                static void Use(int value)
                {
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAThisQualifiedField()
    {
        return AnalyzerTestRunner.VerifyAsync<DeadFieldStoreAnalyzer>("""
            class C
            {
                private int _value;

                void M()
                {
                    {|E128250:this._value = 1|};
                    this._value = 2;
                    Use(this._value);
                }

                static void Use(int value)
                {
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_InsideANestedBlock()
    {
        return AnalyzerTestRunner.VerifyAsync<DeadFieldStoreAnalyzer>("""
            class C
            {
                private int _value;

                void M(bool flag)
                {
                    if (flag)
                    {
                        {|E128250:_value = 1|};
                        _value = 2;
                        Use(_value);
                    }
                }

                static void Use(int value)
                {
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAStaticPrivateField()
    {
        return AnalyzerTestRunner.VerifyAsync<DeadFieldStoreAnalyzer>("""
            class C
            {
                private static int _value;

                static void M()
                {
                    {|E128250:_value = 1|};
                    _value = 2;
                    Use(_value);
                }

                static void Use(int value)
                {
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheValueIsReadBetweenTheWrites()
    {
        return AnalyzerTestRunner.VerifyAsync<DeadFieldStoreAnalyzer>("""
            class C
            {
                private int _value;

                void M()
                {
                    _value = 1;
                    Use(_value);
                    _value = 2;
                }

                static void Use(int value)
                {
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenACallSitsBetweenTheWrites()
    {
        return AnalyzerTestRunner.VerifyAsync<DeadFieldStoreAnalyzer>("""
            class C
            {
                private int _value;

                void M()
                {
                    _value = 1;
                    Log();
                    _value = 2;
                }

                void Log()
                {
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAPublicField()
    {
        return AnalyzerTestRunner.VerifyAsync<DeadFieldStoreAnalyzer>("""
            class C
            {
                public int Value;

                void M()
                {
                    Value = 1;
                    Value = 2;
                    Use(Value);
                }

                static void Use(int value)
                {
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForALocalVariable()
    {
        return AnalyzerTestRunner.VerifyAsync<DeadFieldStoreAnalyzer>("""
            class C
            {
                void M()
                {
                    var value = 1;
                    value = 2;
                    Use(value);
                }

                static void Use(int value)
                {
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenOnlyOneBranchOverwrites()
    {
        return AnalyzerTestRunner.VerifyAsync<DeadFieldStoreAnalyzer>("""
            class C
            {
                private int _value;

                void M(bool flag)
                {
                    _value = 1;
                    if (flag)
                    {
                        _value = 2;
                    }

                    Use(_value);
                }

                static void Use(int value)
                {
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForTheFinalAssignment()
    {
        return AnalyzerTestRunner.VerifyAsync<DeadFieldStoreAnalyzer>("""
            class C
            {
                private int _value;

                void M()
                {
                    _value = 1;
                }
            }
            """);
    }
}

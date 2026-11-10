using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed partial class ImplicitStateMachineAnalyzerTests
{
    private static Task VerifyAsync(string code, string? minFlags = null)
    {
        var test = new CSharpAnalyzerTest<ImplicitStateMachineAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };

        if (minFlags is not null)
        {
            test.TestState.AnalyzerConfigFiles.Add(
                ("/.editorconfig", "is_global = true\ne128_min_state_flags = " + minFlags));
        }

        return test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ThreeFlagsCoWritten_Fires()
    {
        return VerifyAsync("""
                           sealed class {|E128218:Player|}
                           {
                               private bool _isIdle;
                               private bool _isRunning;
                               private bool _isPaused;

                               public void Start()
                               {
                                   _isIdle = false;
                                   _isRunning = true;
                               }

                               public void Stop()
                               {
                                   _isRunning = false;
                                   _isIdle = true;
                               }

                               public void Pause()
                               {
                                   _isRunning = false;
                                   _isPaused = true;
                               }

                               public void Resume()
                               {
                                   _isPaused = false;
                                   _isRunning = true;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ThreeFlagsNoEvidence_Silent()
    {
        return VerifyAsync("""
                           sealed class Flags
                           {
                               private bool _a;
                               private bool _b;
                               private bool _c;

                               public bool A => _a;
                               public bool B => _b;
                               public bool C => _c;
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ThreeFlagsWrittenSeparately_Silent()
    {
        return VerifyAsync("""
                           sealed class Flags
                           {
                               private bool _a;
                               private bool _b;
                               private bool _c;

                               public void ToggleA() { _a = !_a; }
                               public void ToggleB() { _b = !_b; }
                               public void ToggleC() { _c = !_c; }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FourFlagsCoRead_Fires()
    {
        return VerifyAsync("""
                           sealed class {|E128218:Machine|}
                           {
                               private bool _a;
                               private bool _b;
                               private bool _c;
                               private bool _d;

                               public void SetA(bool value) { _a = value; }
                               public void SetB(bool value) { _b = value; }
                               public void SetC(bool value) { _c = value; }
                               public void SetD(bool value) { _d = value; }

                               public void Run()
                               {
                                   if (_a && !_b)
                                   {
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task IndependentFlagsCoRead_Silent()
    {
        return VerifyAsync("""
                           sealed class Machine
                           {
                               private bool _a;
                               private bool _b;
                               private bool _c;
                               private bool _d;

                               public void SetA(bool value) { _a = value; }
                               public void SetB(bool value) { _b = value; }
                               public void SetC(bool value) { _c = value; }
                               public void SetD(bool value) { _d = value; }

                               public void Run()
                               {
                                   if (_a && _b)
                                   {
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task EqualityNegatedCoRead_Fires()
    {
        return VerifyAsync("""
                           sealed class {|E128218:Machine|}
                           {
                               private bool _a;
                               private bool _b;
                               private bool _c;
                               private bool _d;

                               public void SetA(bool value) { _a = value; }
                               public void SetB(bool value) { _b = value; }
                               public void SetC(bool value) { _c = value; }
                               public void SetD(bool value) { _d = value; }

                               public void Run()
                               {
                                   if (_a == false && _b)
                                   {
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task IsFalsePatternCoRead_Fires()
    {
        return VerifyAsync("""
                           sealed class {|E128218:Machine|}
                           {
                               private bool _a;
                               private bool _b;
                               private bool _c;
                               private bool _d;

                               public void SetA(bool value) { _a = value; }
                               public void SetB(bool value) { _b = value; }
                               public void SetC(bool value) { _c = value; }
                               public void SetD(bool value) { _d = value; }

                               public void Run()
                               {
                                   if (_a is false && _b)
                                   {
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ThreeFlagsCoRead_Silent()
    {
        return VerifyAsync("""
                           sealed class Machine
                           {
                               private bool _a;
                               private bool _b;
                               private bool _c;

                               public void SetA(bool value) { _a = value; }
                               public void SetB(bool value) { _b = value; }
                               public void SetC(bool value) { _c = value; }

                               public void Run()
                               {
                                   if (_a && !_b)
                                   {
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task WhileConditionCoRead_Fires()
    {
        return VerifyAsync("""
                           sealed class {|E128218:Machine|}
                           {
                               private bool _a;
                               private bool _b;
                               private bool _c;
                               private bool _d;
                               private bool _e;

                               public void SetA(bool value) { _a = value; }
                               public void SetB(bool value) { _b = value; }
                               public void SetC(bool value) { _c = value; }
                               public void SetD(bool value) { _d = value; }
                               public void SetE(bool value) { _e = value; }

                               public void Run()
                               {
                                   while (_a && !_b)
                                   {
                                       _c = false;
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TwoFlagsCoWritten_Default_Silent()
    {
        return VerifyAsync("""
                           sealed class Pair
                           {
                               private bool _a;
                               private bool _b;

                               public void SelectA()
                               {
                                   _a = true;
                                   _b = false;
                               }

                               public void SelectB()
                               {
                                   _a = false;
                                   _b = true;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TwoFlagsCoWritten_OptionTwo_Fires()
    {
        return VerifyAsync("""
                           sealed class {|E128218:Pair|}
                           {
                               private bool _a;
                               private bool _b;

                               public void SelectA()
                               {
                                   _a = true;
                                   _b = false;
                               }

                               public void SelectB()
                               {
                                   _a = false;
                                   _b = true;
                               }
                           }
                           """, "2");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TwoFlagsCoWritten_ProjectEditorConfig_Fires()
    {
        var test = new CSharpAnalyzerTest<ImplicitStateMachineAnalyzer, DefaultVerifier>
        {
            TestCode = """
                       sealed class {|E128218:Pair|}
                       {
                           private bool _a;
                           private bool _b;

                           public void SelectA()
                           {
                               _a = true;
                               _b = false;
                           }

                           public void SelectB()
                           {
                               _a = false;
                               _b = true;
                           }
                       }
                       """,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };

        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", "[*.cs]\ne128_min_state_flags = 2"));

        return test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ResetOnly_Silent()
    {
        return VerifyAsync("""
                           sealed class Flags
                           {
                               private bool _a;
                               private bool _b;
                               private bool _c;

                               public void ToggleA() { _a = !_a; }
                               public void ToggleB() { _b = !_b; }
                               public void ToggleC() { _c = !_c; }

                               public void Reset()
                               {
                                   _a = false;
                                   _b = false;
                                   _c = false;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DisposalFlagDoesNotInflateCluster_Silent()
    {
        return VerifyAsync("""
                           sealed class Resource
                           {
                               private bool _isDisposed;
                               private bool _a;
                               private bool _b;

                               public bool IsDisposed => _isDisposed;

                               public void Dispose()
                               {
                                   _isDisposed = true;
                                   _a = false;
                                   _b = false;
                               }

                               public void Reset()
                               {
                                   _a = true;
                                   _b = true;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ReadonlyConstStatic_Silent()
    {
        return VerifyAsync("""
                           sealed class Flags
                           {
                               private readonly bool _a = true;
                               private const bool _b = true;
                               private static bool _c;

                               public bool A => _a;
                               public bool B => _b;
                               public static bool C => _c;

                               public void Set()
                               {
                                   _c = true;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NullableBool_Silent()
    {
        return VerifyAsync("""
                           sealed class Flags
                           {
                               private bool? _a;
                               private bool? _b;
                               private bool? _c;

                               public bool? A => _a;
                               public bool? B => _b;
                               public bool? C => _c;

                               public void Set()
                               {
                                   _a = true;
                                   _b = false;
                                   _c = true;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Record_Silent()
    {
        return VerifyAsync("""
                           record Flags
                           {
                               private bool _a;
                               private bool _b;
                               private bool _c;

                               public bool A => _a;
                               public bool B => _b;
                               public bool C => _c;

                               public void Set()
                               {
                                   _a = true;
                                   _b = false;
                                   _c = true;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Struct_Silent()
    {
        return VerifyAsync("""
                           struct Flags
                           {
                               private bool _a;
                               private bool _b;
                               private bool _c;

                               public bool A => _a;
                               public bool B => _b;
                               public bool C => _c;

                               public void Set()
                               {
                                   _a = true;
                                   _b = false;
                                   _c = true;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConstructorOnlyWrites_Silent()
    {
        return VerifyAsync("""
                           sealed class Flags
                           {
                               private bool _a;
                               private bool _b;
                               private bool _c;

                               public Flags()
                               {
                                   _a = true;
                                   _b = false;
                                   _c = true;
                               }

                               public bool A => _a;
                               public bool B => _b;
                               public bool C => _c;
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LambdaWritesAreSeparateBodies_Silent()
    {
        return VerifyAsync("""
                           sealed class Flags
                           {
                               private bool _a;
                               private bool _b;
                               private bool _c;

                               public void Setup()
                               {
                                   System.Action first = () => _a = !_a;
                                   System.Action second = () => _b = !_b;
                                   System.Action third = () => _c = !_c;
                                   first();
                                   second();
                                   third();
                               }
                           }
                           """);
    }
}

using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ImplicitStateMachineAnalyzerTests
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

        return test.RunAsync();
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

        return test.RunAsync();
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

    [Fact]
    [Trait("Category", "CI")]
    public Task LambdaMixedCoWrite_Fires()
    {
        return VerifyAsync("""
                           sealed class {|E128218:Flags|}
                           {
                               private bool _a;
                               private bool _b;
                               private bool _c;

                               public void Setup()
                               {
                                   System.Action first = () =>
                                   {
                                       _a = true;
                                       _b = false;
                                   };

                                   System.Action second = () =>
                                   {
                                       _a = false;
                                       _b = true;
                                       _c = !_c;
                                   };

                                   first();
                                   second();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LocalFunctionMixedCoWrite_Fires()
    {
        return VerifyAsync("""
                           sealed class {|E128218:Flags|}
                           {
                               private bool _a;
                               private bool _b;
                               private bool _c;

                               public void Setup()
                               {
                                   void First()
                                   {
                                       _a = true;
                                       _b = false;
                                   }

                                   void Second()
                                   {
                                       _a = false;
                                       _b = true;
                                       _c = !_c;
                                   }

                                   First();
                                   Second();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ObsoleteField_Silent()
    {
        return VerifyAsync("""
                           sealed class Flags
                           {
                               [System.Obsolete]
                               private bool _a;

                               private bool _b;
                               private bool _c;

                               public void Set()
                               {
                                   _a = !_a;
                                   _b = !_b;
                                   _c = !_c;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PropertyState_Fires()
    {
        return VerifyAsync("""
                           sealed class {|E128218:UiState|}
                           {
                               public bool IsLoading { get; set; }
                               public bool HasError { get; set; }
                               public bool IsComplete { get; set; }

                               public void Load()
                               {
                                   IsLoading = true;
                                   HasError = false;
                                   IsComplete = false;
                               }

                               public void Fail()
                               {
                                   IsLoading = false;
                                   HasError = true;
                                   IsComplete = false;
                               }

                               public void Complete()
                               {
                                   IsLoading = false;
                                   HasError = false;
                                   IsComplete = true;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PropertyExternalWrites_Silent()
    {
        return VerifyAsync("""
                           sealed class Options
                           {
                               public bool UseCache { get; set; }
                               public bool UseRetry { get; set; }
                               public bool UseCompression { get; set; }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ExplicitPropertyBodies_Silent()
    {
        return VerifyAsync("""
                           sealed class Flags
                           {
                               private bool _a;
                               private bool _b;
                               private bool _c;

                               public bool A { get => _a; set => _a = value; }
                               public bool B { get => _b; set => _b = value; }
                               public bool C { get => _c; set => _c = value; }

                               public void Set(bool value)
                               {
                                   A = value;
                                   B = value;
                                   C = value;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task VolatileWriteCoWrite_Fires()
    {
        return VerifyAsync("""
                           using System.Threading;

                           sealed class {|E128218:Machine|}
                           {
                               private bool _a;
                               private bool _b;
                               private bool _c;

                               public void SelectA()
                               {
                                   Volatile.Write(ref _a, true);
                                   Volatile.Write(ref _b, false);
                               }

                               public void SelectB()
                               {
                                   Volatile.Write(ref _a, false);
                                   Volatile.Write(ref _b, true);
                                   Volatile.Write(ref _c, !_c);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task InterlockedExchangeCoWrite_Fires()
    {
        return VerifyAsync("""
                           using System.Threading;

                           sealed class {|E128218:Machine|}
                           {
                               private bool _a;
                               private bool _b;
                               private bool _c;

                               public void SelectA()
                               {
                                   Interlocked.Exchange(ref _a, true);
                                   Interlocked.Exchange(ref _b, false);
                               }

                               public void SelectB()
                               {
                                   Interlocked.Exchange(ref _a, false);
                                   Interlocked.Exchange(ref _b, true);
                                   Interlocked.Exchange(ref _c, !_c);
                               }
                           }
                           """);
    }

    private const string Corpus = """
                                  sealed class {|E128218:Upload|}
                           {
                               private bool _isIdle;
                               private bool _isUploading;
                               private bool _isPaused;

                               public void Start()  { _isIdle = false; _isUploading = true; }
                               public void Pause()  { _isUploading = false; _isPaused = true; }
                               public void Resume() { _isPaused = false; _isUploading = true; }
                               public void Stop()   { _isUploading = false; _isIdle = true; }
                           }

                           sealed class {|E128218:Connection|}
                           {
                               private bool _isConnecting;
                               private bool _isOpen;
                               private bool _isFaulted;

                               public void Connect()
                               {
                                   _isConnecting = true;
                                   _isOpen = false;
                                   _isFaulted = false;
                               }

                               public void Open()
                               {
                                   _isConnecting = false;
                                   _isOpen = true;
                                   _isFaulted = false;
                               }

                               public void Fault()
                               {
                                   _isConnecting = false;
                                   _isOpen = false;
                                   _isFaulted = true;
                               }

                               public void Reset()
                               {
                                   _isFaulted = false;
                                   _isConnecting = false;
                               }
                           }

                           sealed class {|E128218:UiState|}
                           {
                               public bool IsLoading { get; set; }
                               public bool HasError { get; set; }
                               public bool IsComplete { get; set; }

                               public void Load()
                               {
                                   IsLoading = true;
                                   HasError = false;
                                   IsComplete = false;
                               }

                               public void Fail()
                               {
                                   IsLoading = false;
                                   HasError = true;
                                   IsComplete = false;
                               }

                               public void Complete()
                               {
                                   IsLoading = false;
                                   HasError = false;
                                   IsComplete = true;
                               }
                           }

                           sealed class FeatureFlags
                           {
                               private bool _useCache;
                               private bool _useRetry;
                               private bool _useCompression;
                               private bool _trace;

                               public void SetCache(bool value) { _useCache = value; }
                               public void SetRetry(bool value) { _useRetry = value; }
                               public void SetCompression(bool value) { _useCompression = value; }
                               public void SetTrace(bool value) { _trace = value; }

                               public void Run()
                               {
                                   if (_useCache && _useRetry)
                                   {
                                   }
                               }
                           }

                           sealed class Options
                           {
                               public bool UseCache { get; set; }
                               public bool UseRetry { get; set; }
                               public bool UseCompression { get; set; }
                           }

                           sealed class ResetOnly
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
                           """;

    [Fact]
    [Trait("Category", "CI")]
    public Task LabeledCorpus_MatchesPrecisionAndRecall()
    {
        return VerifyAsync(Corpus);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PartialType_Fires()
    {
        return VerifyAsync("""
                           partial class {|E128218:Machine|}
                           {
                               private bool _a;
                               private bool _b;
                               private bool _c;
                           }

                           partial class Machine
                           {
                               public void SelectA()
                               {
                                   _a = true;
                                   _b = false;
                               }

                               public void SelectB()
                               {
                                   _a = false;
                                   _b = true;
                                   _c = !_c;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task GenericType_Fires()
    {
        return VerifyAsync("""
                           sealed class {|E128218:Machine|}<T>
                           {
                               private bool _a;
                               private bool _b;
                               private bool _c;

                               public T? Value { get; set; }

                               public void SelectA()
                               {
                                   _a = true;
                                   _b = false;
                               }

                               public void SelectB()
                               {
                                   _a = false;
                                   _b = true;
                                   _c = !_c;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConstructorThenTransitions_Fires()
    {
        return VerifyAsync("""
                           sealed class {|E128218:Machine|}
                           {
                               private bool _a;
                               private bool _b;
                               private bool _c;

                               public Machine()
                               {
                                   _a = true;
                                   _b = false;
                                   _c = false;
                               }

                               public void SelectA()
                               {
                                   _a = true;
                                   _b = false;
                               }

                               public void SelectB()
                               {
                                   _a = false;
                                   _b = true;
                                   _c = !_c;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PropertyResetOnly_Silent()
    {
        return VerifyAsync("""
                           sealed class Settings
                           {
                               public bool UseCache { get; set; }
                               public bool UseRetry { get; set; }
                               public bool UseCompression { get; set; }

                               public void Enable()
                               {
                                   UseCache = true;
                                   UseRetry = true;
                                   UseCompression = true;
                               }

                               public void Disable()
                               {
                                   UseCache = false;
                                   UseRetry = false;
                                   UseCompression = false;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PropertyAccessorKinds_Silent()
    {
        return VerifyAsync("""
                           sealed class Flags
                           {
                               public bool InitOnly { get; init; }
                               public bool GetOnly { get; }
                               public static bool StaticFlag { get; set; }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AbstractProperties_Silent()
    {
        return VerifyAsync("""
                           abstract class Flags
                           {
                               public abstract bool A { get; set; }
                               public abstract bool B { get; set; }
                               public abstract bool C { get; set; }

                               public void Set()
                               {
                                   A = true;
                                   B = false;
                                   C = true;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FieldInitializersOnly_Silent()
    {
        return VerifyAsync("""
                           sealed class Flags
                           {
                               private bool _a = true;
                               private bool _b = false;
                               private bool _c = true;

                               public bool A => _a;
                               public bool B => _b;
                               public bool C => _c;
                           }
                           """);
    }
}

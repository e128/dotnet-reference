using System.Threading.Tasks;
using E128.Analyzers.Design;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ImplicitStateMachineAnalyzerBranchCoverageGapTests
{
    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<ImplicitStateMachineAnalyzer>(code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAWriteTargetsAFieldOfAMetadataType()
    {
        return VerifyAsync("""
            sealed class Machine
            {
                private bool _a;
                private bool _b;
                private bool _c;

                public void Run()
                {
                    var pair = (false, false);
                    pair.Item1 = true;
                    pair.Item2 = false;
                    _ = pair;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForInterlockedWritesInATypeThatIsNotAnalyzed()
    {
        return VerifyAsync("""
            using System.Threading;

            sealed record Gate
            {
                private bool _a;
                private bool _b;
                private bool _c;

                public void Open()
                {
                    Interlocked.Exchange(ref _a, true);
                    Interlocked.Exchange(ref _b, false);
                    Interlocked.Exchange(ref _c, true);
                }

                public void Close()
                {
                    Interlocked.Exchange(ref _a, false);
                    Interlocked.Exchange(ref _b, true);
                    Interlocked.Exchange(ref _c, false);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenConditionPatternsDoNotNegateAFlag()
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
                    if (_a && _b is bool seen && _c && _d is true or false)
                    {
                        _ = seen;
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAFlagCarriesAnAttributeThatIsNotObsolete()
    {
        return VerifyAsync("""
            sealed class {|E128218:Machine|}
            {
                [System.NonSerialized]
                private bool _a;
                private bool _b;
                private bool _c;

                public void Merge(bool x)
                {
                    _a |= x;
                    _b &= x;
                    _c |= x;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenFlagsOutsideTheClusterAreOnlySetOneWay()
    {
        return VerifyAsync("""
            sealed class Machine
            {
                private bool _a;
                private bool _b;
                private bool _onlyTrue;
                private bool _onlyFalse;

                public void Start() { _a = true; _b = false; _onlyTrue = true; _onlyFalse = false; }
                public void Stop() { _a = false; _b = true; _onlyTrue = true; _onlyFalse = false; }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAForwardingAccessorTouchesAnotherInstance()
    {
        return VerifyAsync("""
            sealed class Machine
            {
                private readonly Machine _other = new();
                private bool _a;
                private bool _b;
                private bool _c;

                public bool A { get => _other._a; set => _other._a = value; }

                public void X() { A = true; _b = true; _c = true; }
                public void Y() { A = false; _b = false; _c = false; }
            }
            """);
    }
}

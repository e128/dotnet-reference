using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UseAfterDisposeBranchCoverageTests
{
    private static Task VerifyAsync(string body)
    {
        return AnalyzerTestRunner.VerifyAsync<UseAfterDisposeAnalyzer>(
            "using System;\nusing System.IO;\nclass Res : IDisposable { public int Value; public void Dispose() { } public void Dispose(bool disposing) { } }\n"
            + "class C\n{\n    Res _field = new();\n    void M(bool flag)\n    {\n" + body + "\n    }\n}\n");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheDisposedReceiverIsAFieldOrAParameter()
    {
        return VerifyAsync("""
                    _field.Dispose();
                    var length = _field.Value;
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForADisposeOverloadWithArguments()
    {
        return VerifyAsync("""
                    var res = new Res();
                    res.Dispose(true);
                    var value = res.Value;
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheDisposeIsInsideABracelessIf()
    {
        return VerifyAsync("""
                    var res = new Res();
                    if (flag) res.Dispose();
                    var value = res.Value;
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAMemberReadAfterTheDispose()
    {
        return VerifyAsync("""
                    var res = new Res();
                    res.Dispose();
                    var value = {|E128162:res|}.Value;
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLocalIsReassignedBeforeTheNextRead()
    {
        return VerifyAsync("""
                    var res = new Res();
                    res.Dispose();
                    res = new Res();
                    var value = res.Value;
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForNonMemberUsesAfterTheDispose()
    {
        return VerifyAsync("""
                    var res = new Res();
                    res.Dispose();
                    var name = nameof(res.Value);
                    var isNull = res is null;
                    var same = res == null;
                    res.Dispose();
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenALambdaOrLocalFunctionReadsTheLocal()
    {
        return VerifyAsync("""
                    var res = new Res();
                    res.Dispose();
                    Func<int> read = () => res.Value;
                    int Local() => res.Value;
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAnIgnorableUseSitsBeforeTheMemberRead()
    {
        return VerifyAsync("""
                    var res = new Res();
                    res.Dispose();
                    var isNull = res is null;
                    var value = {|E128162:res|}.Value;
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForADisposeOnAnUnrelatedInvocationShape()
    {
        return VerifyAsync("""
                    var res = new Res();
                    Dispose();
                    Action action = res.Dispose;
                    var stream = new MemoryStream();
                    stream.Dispose();

                    static void Dispose() { }
            """);
    }
}

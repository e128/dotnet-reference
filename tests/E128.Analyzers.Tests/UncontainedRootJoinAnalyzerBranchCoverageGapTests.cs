using System.Threading.Tasks;
using E128.Analyzers.Security;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UncontainedRootJoinAnalyzerBranchCoverageGapTests
{
    private const string Guards = """
        static class Guard
        {
            public static bool IsInside(object path) => true;
        }

        class Holder
        {
            public string Name = "";
        }
        """;

    private static Task VerifyAsync(string code, AnalyzerTestOptions? options = null)
    {
        return AnalyzerTestRunner.VerifyAsync<UncontainedRootJoinAnalyzer>(
            code + "\n" + Guards,
            expected: null,
            options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCombineReceiverCannotBeBound()
    {
        return VerifyAsync(
            """
            using System.IO;
            class C
            {
                string M(DirectoryInfo root, string leaf)
                {
                    return Missing.Combine(root.FullName, leaf);
                }
            }
            """,
            new AnalyzerTestOptions { CompilerDiagnostics = CompilerDiagnostics.None });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAnInterpolationHoleHasNoType()
    {
        return VerifyAsync("""
            using System.IO;
            class C
            {
                string M(DirectoryInfo root)
                {
                    return {|E128222:Path.Combine(root.FullName, $"{null}")|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAssignmentsBeforeTheGuardCarryOtherValues()
    {
        return VerifyAsync("""
            using System.IO;
            class C
            {
                void M(DirectoryInfo root, string leaf, Holder holder)
                {
                    string other;
                    other = leaf;
                    holder.Name = leaf;
                    var joined = {|E128222:Path.Combine(root.FullName, leaf)|};
                    Guard.IsInside(other);
                    Guard.IsInside(holder.Name);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheLeafIsAGenericOrDelegateCall()
    {
        return VerifyAsync("""
            using System;
            using System.IO;
            class C
            {
                static T Identity<T>(T value) => value;

                void M(DirectoryInfo root, string leaf, Func<string, string>[] maps)
                {
                    var first = {|E128222:Path.Combine(root.FullName, Identity<string>(leaf))|};
                    var second = {|E128222:Path.Combine(root.FullName, maps[0](leaf))|};
                }
            }
            """);
    }
}

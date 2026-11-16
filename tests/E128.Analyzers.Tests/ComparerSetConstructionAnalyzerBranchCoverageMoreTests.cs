using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ComparerSetConstructionAnalyzerBranchCoverageMoreTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheSetIsAFieldOrPropertyInitializer()
    {
        return AnalyzerTestRunner.VerifyAsync<ComparerSetConstructionAnalyzer>("""
            using System;
            using System.Collections.Generic;

            class C
            {
                private readonly HashSet<string> field = new(StringComparer.Ordinal);

                HashSet<string> Property { get; } = new(StringComparer.Ordinal);

                void M(string[] items)
                {
                    foreach (var item in items)
                    {
                        field.Add(item);
                        Property.Add(item);
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheSetIsAssignedToAFieldOrAMemberAccess()
    {
        return AnalyzerTestRunner.VerifyAsync<ComparerSetConstructionAnalyzer>("""
            using System;
            using System.Collections.Generic;

            class C
            {
                private HashSet<string> field = new();

                void M(string[] items)
                {
                    field = new HashSet<string>(StringComparer.Ordinal);
                    this.field = new HashSet<string>(StringComparer.Ordinal);

                    foreach (var item in items)
                    {
                        field.Add(item);
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheSetIsAnArgument()
    {
        return AnalyzerTestRunner.VerifyAsync<ComparerSetConstructionAnalyzer>("""
            using System;
            using System.Collections.Generic;

            class C
            {
                static void Consume(HashSet<string> set)
                {
                }

                void M()
                {
                    Consume(new HashSet<string>(StringComparer.Ordinal));
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheConstructorArgumentHasNoType()
    {
        return AnalyzerTestRunner.VerifyAsync<ComparerSetConstructionAnalyzer>(
            """
            using System.Collections.Generic;

            class C
            {
                void M(string[] items)
                {
                    var set = new HashSet<string>(null);
                    foreach (var item in items)
                    {
                        set.Add(item);
                    }
                }
            }
            """,
            options: new AnalyzerTestOptions { CompilerDiagnostics = CompilerDiagnostics.None });
    }
}

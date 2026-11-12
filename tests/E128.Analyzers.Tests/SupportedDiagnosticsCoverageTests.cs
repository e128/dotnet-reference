using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SupportedDiagnosticsCoverageTests
{
    /// <summary>
    /// Reflection is required: this sweep enumerates every analyzer type and its
    /// DiagnosticDescriptor fields, so a descriptor that never reaches
    /// SupportedDiagnostics fails a test instead of shipping silent. See the
    /// GeneratedRegexAnalyzer 2.9.7 audit in plans/roadmap.md. The reflection
    /// lives in helper methods, not in the test body, because E128090 bans
    /// reflection inside test methods.
    /// </summary>
    public static TheoryData<Type> AnalyzerTypes()
    {
        var assembly = typeof(Reliability.GeneratedRegexAnalyzer).Assembly;
        var data = new TheoryData<Type>();
        var analyzers = assembly.GetTypes()
            .Where(static t => t is { IsAbstract: false, IsInterface: false }
                && typeof(DiagnosticAnalyzer).IsAssignableFrom(t))
            .Order(TypeComparer.Instance);

        foreach (var analyzer in analyzers)
        {
            data.Add(analyzer);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AnalyzerTypes), MemberType = typeof(SupportedDiagnosticsCoverageTests))]
    [Trait("Category", "CI")]
    public void SupportedDiagnostics_CoversEveryDeclaredDescriptor(Type analyzerType)
    {
        ArgumentNullException.ThrowIfNull(analyzerType);

        var supported = CreateAnalyzer(analyzerType).SupportedDiagnostics;

        Assert.NotEmpty(supported);

        var supportedIds = supported.Select(static d => d.Id).ToHashSet(StringComparer.Ordinal);
        var declaredIds = GetDeclaredDescriptorIds(analyzerType);

        Assert.NotEmpty(declaredIds);

        var missing = declaredIds.Except(supportedIds, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

        Assert.True(
            missing.Length is 0,
            $"{analyzerType.Name} declares {string.Join(", ", missing)} but never returns them from SupportedDiagnostics, so those rules cannot report.");
    }

    private static DiagnosticAnalyzer CreateAnalyzer(Type analyzerType)
    {
        return (DiagnosticAnalyzer)Activator.CreateInstance(analyzerType)!;
    }

    private static HashSet<string> GetDeclaredDescriptorIds(Type analyzerType)
    {
        var fields = analyzerType.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var field in fields)
        {
            if (field.FieldType.Equals(typeof(DiagnosticDescriptor)))
            {
                ids.Add(((DiagnosticDescriptor)field.GetValue(null)!).Id);
            }
        }

        return ids;
    }

    private sealed class TypeComparer : IComparer<Type>
    {
        internal static readonly TypeComparer Instance = new();

        public int Compare(Type? x, Type? y)
        {
            return string.CompareOrdinal(x?.FullName, y?.FullName);
        }
    }
}

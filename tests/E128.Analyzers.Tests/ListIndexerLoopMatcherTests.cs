using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

/// <summary>
///     Direct checks on the E128084 indexer-loop matcher, against a real compilation.
///     The analyzer tests cover the diagnostic; this test shows which shape a change breaks.
/// </summary>
public sealed class ListIndexerLoopMatcherTests
{
    private const string Source = """
                                  using System.Collections.Generic;
                                  class C
                                  {
                                      int Total(List<string> items)
                                      {
                                          int total = 0;
                                          for (int i = 0; i < items.Count; i++)
                                          {
                                              total += items[i].Length;
                                          }
                                          return total;
                                      }
                                  }
                                  """;

    [Fact]
    [Trait("Category", "CI")]
    public async Task Matches_WhenTheLoopOnlyReadsTheIndexer()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tree = CSharpSyntaxTree.ParseText(Source, cancellationToken: cancellationToken);
        var references = await ReferenceAssemblies.Net.Net100.ResolveAsync(LanguageNames.CSharp, cancellationToken);
        var compilation = CSharpCompilation.Create(
            "Probe",
            [tree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var model = compilation.GetSemanticModel(tree);
        var root = await tree.GetRootAsync(cancellationToken);
        var loop = root.DescendantNodes().OfType<ForStatementSyntax>().Single();
        var listType = compilation.GetTypeByMetadataName("System.Collections.Generic.List`1");

        Assert.NotNull(listType);
        Assert.True(IndexedForLoop.TryMatch(loop, "Count", out var variable, out var owner, out _));
        Assert.Equal("i", variable);
        Assert.NotNull(owner);

        var listSymbol = model.GetSymbolInfo(owner, cancellationToken).Symbol;
        var loopVariable = GetLoopVariable(loop, model, cancellationToken);
        Assert.True(loopVariable is ILocalSymbol);

        CheckIdentifiers(loop, model, loopVariable, listSymbol, cancellationToken);

        var matched = ListGetRangeAnalyzer.TryMatchIndexerLoop(
            loop,
            model,
            listType,
            cancellationToken,
            out _,
            out var reads);

        Assert.True(matched, "matcher failed");
        var single = Assert.Single(reads);
        Assert.NotNull(single);
    }

    private static ISymbol? GetLoopVariable(
        ForStatementSyntax loop,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        return loop.Declaration is { Variables.Count: 1 } declaration
            ? model.GetDeclaredSymbol(declaration.Variables[0], cancellationToken)
            : null;
    }

    private static void CheckIdentifiers(
        ForStatementSyntax loop,
        SemanticModel model,
        ISymbol? loopVariable,
        ISymbol? listSymbol,
        CancellationToken cancellationToken)
    {
        foreach (var identifier in loop.Statement.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
        {
            var symbol = model.GetSymbolInfo(identifier, cancellationToken).Symbol;

            if (SymbolEqualityComparer.Default.Equals(symbol, loopVariable))
            {
                Assert.True(identifier.Parent is ArgumentSyntax);
                Assert.True(identifier.Parent.Parent?.Parent is ElementAccessExpressionSyntax);
                var element = (ElementAccessExpressionSyntax)identifier.Parent.Parent.Parent;
                var argument = Assert.Single(element.ArgumentList.Arguments);
                Assert.True(argument.Expression.Span == identifier.Span);
                Assert.True(element.Parent is MemberAccessExpressionSyntax);
                Assert.True(SymbolEqualityComparer.Default.Equals(
                    model.GetSymbolInfo(element.Expression, cancellationToken).Symbol,
                    listSymbol));
            }

            if (SymbolEqualityComparer.Default.Equals(symbol, listSymbol))
            {
                Assert.True(identifier.Parent is ElementAccessExpressionSyntax);
            }
        }
    }
}

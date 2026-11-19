using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128240: A <c lang="csharp">lock</c> body holds a blocking call. The lock stays held for
///     the whole call, so another thread waits and the pool starves.
/// </summary>
/// <remarks>
///     The rule reports <c lang="csharp">Thread.Sleep</c>, a <c lang="csharp">File</c> call, a
///     blocking <c lang="csharp">Stream</c> member, and the synchronous
///     <c lang="csharp">DbCommand</c> execute family. A <c lang="csharp">MemoryStream</c>
///     member stays silent because it completes without I/O. A nested lock, a lambda, and a
///     local function stay out of the outer scan. A nested lock reports its own calls, and the
///     statement order does not describe when an inner body runs.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class BlockingCallInLockAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128240";

    private static readonly ImmutableHashSet<string> BlockingStreamMembers = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "CopyTo",
        "EndRead",
        "EndWrite",
        "Flush",
        "Read",
        "ReadAtLeast",
        "ReadByte",
        "ReadExactly",
        "Seek",
        "SetLength",
        "Write",
        "WriteByte");

    private static readonly ImmutableHashSet<string> BlockingDbCommandMembers = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "ExecuteNonQuery",
        "ExecuteReader",
        "ExecuteScalar",
        "ExecuteXmlReader",
        "Prepare");

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Blocking call inside a lock",
        "Blocking call '{0}' inside a lock holds the lock for the whole call",
        "E128-Reliability",
        DiagnosticSeverity.Warning,
        true,
        "Do the blocking work outside the lock, or use an asynchronous primitive that releases the lock while it waits.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static start =>
        {
            var compilation = start.Compilation;
            var types = new TypeSet(
                compilation.GetTypeByMetadataName("System.Threading.Thread"),
                compilation.GetTypeByMetadataName("System.IO.File"),
                compilation.GetTypeByMetadataName("System.IO.Stream"),
                compilation.GetTypeByMetadataName("System.IO.MemoryStream"),
                compilation.GetTypeByMetadataName("System.Data.Common.DbCommand"));

            if (types.IsEmpty)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => Analyze(nodeContext, types), SyntaxKind.LockStatement);
        });
    }

    private static void Analyze(SyntaxNodeAnalysisContext context, TypeSet types)
    {
        var lockStatement = (LockStatementSyntax)context.Node;

        foreach (var invocation in lockStatement.Statement.DescendantNodesAndSelf(Descend))
        {
            if (invocation is not InvocationExpressionSyntax call
                || context.SemanticModel.GetSymbolInfo(call, context.CancellationToken).Symbol is not IMethodSymbol method
                || !IsBlocking(method, types))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(Rule, call.GetLocation(), method.Name));
        }
    }

    /// <summary>
    ///     Descends through every node except a nested lock, a lambda, and a local function. A
    ///     nested lock reports its own calls, and the statement order does not describe when an
    ///     inner body runs.
    /// </summary>
    private static bool Descend(SyntaxNode node)
    {
        return node is not (LockStatementSyntax or AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax);
    }

    private static bool IsBlocking(IMethodSymbol method, TypeSet types)
    {
        return IsThreadSleep(method, types)
            || IsFileCall(method, types)
            || IsStreamCall(method, types)
            || IsDbCommandCall(method, types);
    }

    private static bool IsThreadSleep(IMethodSymbol method, TypeSet types)
    {
        return types.Thread is { } thread
            && method.IsStatic
            && string.Equals(method.Name, "Sleep", StringComparison.Ordinal)
            && SymbolEqualityComparer.Default.Equals(method.ContainingType, thread);
    }

    private static bool IsFileCall(IMethodSymbol method, TypeSet types)
    {
        return types.File is { } file
            && method.IsStatic
            && SymbolEqualityComparer.Default.Equals(method.ContainingType, file);
    }

    private static bool IsStreamCall(IMethodSymbol method, TypeSet types)
    {
        if (types.Stream is not { } stream
            || method.IsStatic
            || !TypeSymbols.DerivesFrom(method.ContainingType, stream)
            || !BlockingStreamMembers.Contains(method.Name))
        {
            return false;
        }

        // A MemoryStream member completes in memory and holds no lock for I/O.
        return types.MemoryStream is not { } memoryStream
            || !TypeSymbols.DerivesFrom(method.ContainingType, memoryStream);
    }

    private static bool IsDbCommandCall(IMethodSymbol method, TypeSet types)
    {
        return types.DbCommand is { } dbCommand
            && !method.IsStatic
            && TypeSymbols.DerivesFrom(method.ContainingType, dbCommand)
            && BlockingDbCommandMembers.Contains(method.Name);
    }

    private sealed class TypeSet
    {
        internal TypeSet(
            INamedTypeSymbol? thread,
            INamedTypeSymbol? file,
            INamedTypeSymbol? stream,
            INamedTypeSymbol? memoryStream,
            INamedTypeSymbol? dbCommand)
        {
            Thread = thread;
            File = file;
            Stream = stream;
            MemoryStream = memoryStream;
            DbCommand = dbCommand;
        }

        internal INamedTypeSymbol? Thread { get; }

        internal INamedTypeSymbol? File { get; }

        internal INamedTypeSymbol? Stream { get; }

        internal INamedTypeSymbol? MemoryStream { get; }

        internal INamedTypeSymbol? DbCommand { get; }

        internal bool IsEmpty => Thread is null && File is null && Stream is null && DbCommand is null;
    }
}

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace E128.Analyzers.Security;

public sealed partial class UnvalidatedJwtIdentityAnalyzer
{
    private sealed class KnownTypes
    {
        internal KnownTypes(Compilation compilation)
        {
            JwtHandlers = MetadataTypes.Find(compilation, "System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler");
            JwtTokens = MetadataTypes.Find(compilation, "System.IdentityModel.Tokens.Jwt.JwtSecurityToken");
            JsonHandlers = MetadataTypes.Find(compilation, "Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler");
            JsonTokens = MetadataTypes.Find(compilation, "Microsoft.IdentityModel.JsonWebTokens.JsonWebToken");
            Identities = MetadataTypes.Find(compilation, "System.Security.Claims.ClaimsIdentity");
            Principals = MetadataTypes.Find(compilation, "System.Security.Claims.ClaimsPrincipal");
            HttpContexts = MetadataTypes.Find(compilation, "Microsoft.AspNetCore.Http.HttpContext");
            Tickets = MetadataTypes.Find(compilation, "Microsoft.AspNetCore.Authentication.AuthenticationTicket");
        }

        internal ImmutableArray<INamedTypeSymbol> JwtHandlers { get; }
        internal ImmutableArray<INamedTypeSymbol> JwtTokens { get; }
        internal ImmutableArray<INamedTypeSymbol> JsonHandlers { get; }
        internal ImmutableArray<INamedTypeSymbol> JsonTokens { get; }
        internal ImmutableArray<INamedTypeSymbol> Identities { get; }
        internal ImmutableArray<INamedTypeSymbol> Principals { get; }
        internal ImmutableArray<INamedTypeSymbol> HttpContexts { get; }
        internal ImmutableArray<INamedTypeSymbol> Tickets { get; }

        internal bool IsHandler(ITypeSymbol? type)
        {
            return Is(JwtHandlers, type) || Is(JsonHandlers, type);
        }

        internal bool IsToken(ITypeSymbol? type)
        {
            return Is(JwtTokens, type) || Is(JsonTokens, type);
        }
    }

    /// <summary>
    ///     The state of one sink lookup: the decoded token expressions found so far, and the visited nodes
    ///     that stop a cycle through locals or lambda parameters.
    /// </summary>
    private sealed class Search
    {
        private readonly Dictionary<ILocalSymbol, HashSet<(int Position, ValueKind Kind)>> _visited = new(SymbolEqualityComparer.Default);
        private readonly HashSet<IOperation> _lambdas = [];

        internal List<IOperation> Tokens { get; } = [];

        internal bool Visit(ILocalSymbol local, ValueKind kind, int position)
        {
            if (!_visited.TryGetValue(local, out var positions))
            {
                positions = [];
                _visited[local] = positions;
            }

            return positions.Add((position, kind));
        }

        internal bool VisitLambda(IOperation lambda)
        {
            return _lambdas.Add(lambda);
        }
    }

    private sealed class LocalFunctionCalls : OperationWalker
    {
        private readonly HashSet<IMethodSymbol> _roots = new(SymbolEqualityComparer.Default);
        private readonly Dictionary<IMethodSymbol, List<IMethodSymbol>> _calls = new(SymbolEqualityComparer.Default);
        private IMethodSymbol? _current;

        public override void VisitLocalFunction(ILocalFunctionOperation operation)
        {
            var previous = _current;
            _current = operation.Symbol;
            base.VisitLocalFunction(operation);
            _current = previous;
        }

        public override void VisitInvocation(IInvocationOperation operation)
        {
            Record(operation.TargetMethod);
            base.VisitInvocation(operation);
        }

        public override void VisitMethodReference(IMethodReferenceOperation operation)
        {
            Record(operation.Method);
            base.VisitMethodReference(operation);
        }

        public override void VisitNameOf(INameOfOperation operation)
        {
            // A nameof expression does not call or expose the function.
        }

        private void Record(IMethodSymbol method)
        {
            if (method.MethodKind is not MethodKind.LocalFunction)
            {
                return;
            }

            method = method.OriginalDefinition;
            if (_current is null)
            {
                _roots.Add(method);
                return;
            }

            if (!_calls.TryGetValue(_current.OriginalDefinition, out var calls))
            {
                calls = [];
                _calls[_current.OriginalDefinition] = calls;
            }

            calls.Add(method);
        }

        internal HashSet<IMethodSymbol> Reachable()
        {
            var reachable = new HashSet<IMethodSymbol>(_roots, SymbolEqualityComparer.Default);
            var pending = new Queue<IMethodSymbol>(_roots);
            while (pending.Count > 0)
            {
                if (!_calls.TryGetValue(pending.Dequeue(), out var calls))
                {
                    continue;
                }

                foreach (var called in calls.Where(reachable.Add))
                {
                    pending.Enqueue(called);
                }
            }

            return reachable;
        }
    }

    private enum ValueKind
    {
        Principal = 0,
        Identity = 1,
        Claims = 2,
        Token = 3
    }
}

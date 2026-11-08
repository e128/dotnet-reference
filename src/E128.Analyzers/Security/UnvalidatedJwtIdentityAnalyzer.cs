using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace E128.Analyzers.Security;

/// <summary>
///     E128202: Reports decoded JWT claims used to create an authenticated identity for a request.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnvalidatedJwtIdentityAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128202";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Unvalidated JWT claims used as an identity",
        "Claims from a decoded JWT create an authenticated identity used by '{0}'",
        "E128-Security",
        DiagnosticSeverity.Info,
        true,
        "ReadJwtToken, ReadJsonWebToken, ReadToken, and the token constructors decode JWT claims without validating the token. Validate the token before you use its claims to create an authenticated identity.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var types = new KnownTypes(start.Compilation);
            if ((types.JwtHandlers.IsEmpty && types.JsonHandlers.IsEmpty)
                || types.Identities.IsEmpty || types.Principals.IsEmpty
                || (types.HttpContexts.IsEmpty && types.Tickets.IsEmpty))
            {
                return;
            }

            start.RegisterOperationBlockAction(block =>
            {
                foreach (var root in block.OperationBlocks)
                {
                    var calls = new LocalFunctionCalls();
                    calls.Visit(root);
                    var analysis = new BodyAnalysis(types, calls.Reachable());
                    analysis.Visit(root);
                    analysis.Report(block);
                }
            });
        });
    }

    private static bool Is(ImmutableArray<INamedTypeSymbol> types, ITypeSymbol? type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (MetadataTypes.Contains(types, current))
            {
                return true;
            }
        }

        return false;
    }

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
            if (method.MethodKind != MethodKind.LocalFunction)
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

    private sealed class BodyAnalysis(KnownTypes types, HashSet<IMethodSymbol> localFunctions, BodyAnalysis? parent = null, SyntaxNode? origin = null) : OperationWalker
    {
        private readonly HashSet<ILocalSymbol> _declared = new(SymbolEqualityComparer.Default);
        private readonly Dictionary<ILocalSymbol, List<(IOperation Value, SyntaxNode Syntax)>> _definitions = new(SymbolEqualityComparer.Default);
        private readonly Dictionary<ISymbol, List<int>> _writes = new(SymbolEqualityComparer.Default);
        private readonly Dictionary<ISymbol, List<int>> _opaque = new(SymbolEqualityComparer.Default);
        private readonly List<(ISymbol Target, IOperation Value, SyntaxNode Syntax, BodyAnalysis Owner)> _added = [];
        private readonly List<IOperation> _sinks = [];
        private readonly List<IInvocationOperation> _validations = [];
        private readonly List<(SyntaxNode Syntax, ILocalSymbol Result)> _guards = [];
        private readonly List<(SyntaxNode Scope, ILocalSymbol Result)> _passGuards = [];
        private readonly List<BodyAnalysis> _nestedBodies = [];

        public override void VisitAnonymousFunction(IAnonymousFunctionOperation operation)
        {
            VisitNested(operation.Body, operation.Syntax);
        }

        public override void VisitLocalFunction(ILocalFunctionOperation operation)
        {
            if (localFunctions.Contains(operation.Symbol.OriginalDefinition))
            {
                VisitNested(operation.Body, operation.Syntax);
            }
        }

        public override void VisitVariableDeclarator(IVariableDeclaratorOperation operation)
        {
            _declared.Add(operation.Symbol);
            if (operation.Initializer is { } initializer)
            {
                AddDefinition(operation.Symbol, initializer.Value, operation.Syntax);
            }

            base.VisitVariableDeclarator(operation);
        }

        public override void VisitForEachLoop(IForEachLoopOperation operation)
        {
            if (operation.LoopControlVariable is IVariableDeclaratorOperation declarator)
            {
                _declared.Add(declarator.Symbol);
                AddDefinition(declarator.Symbol, operation.Collection, operation.Syntax);
            }

            base.VisitForEachLoop(operation);
        }

        public override void VisitIsPattern(IIsPatternOperation operation)
        {
            RecordPatterns(operation.Pattern, operation.Value, operation.Syntax);

            base.VisitIsPattern(operation);
        }

        public override void VisitSwitch(ISwitchOperation operation)
        {
            foreach (var clause in operation.Cases.SelectMany(@case => @case.Clauses).OfType<IPatternCaseClauseOperation>())
            {
                RecordPatterns(clause.Pattern, operation.Value, clause.Syntax);
            }

            base.VisitSwitch(operation);
        }

        public override void VisitSwitchExpression(ISwitchExpressionOperation operation)
        {
            foreach (var arm in operation.Arms)
            {
                RecordPatterns(arm.Pattern, operation.Value, arm.Syntax);
            }

            base.VisitSwitchExpression(operation);
        }

        private void RecordPatterns(IPatternOperation pattern, IOperation input, SyntaxNode syntax)
        {
            foreach (var declaration in pattern.DescendantsAndSelf().OfType<IDeclarationPatternOperation>())
            {
                if (declaration.DeclaredSymbol is ILocalSymbol local)
                {
                    _declared.Add(local);
                    AddDefinition(local, input, syntax);
                }
            }
        }

        public override void VisitConditional(IConditionalOperation operation)
        {
            var failed = FailedResult(operation.Condition);
            if (failed is not null && operation.WhenFalse is null && Exits(operation.WhenTrue))
            {
                _guards.Add((operation.Syntax, failed));
            }

            if (failed is not null && operation.WhenFalse is { } whenFalse)
            {
                _passGuards.Add((whenFalse.Syntax, failed));
            }

            foreach (var passed in PassedResults(operation.Condition))
            {
                _passGuards.Add((operation.WhenTrue.Syntax, passed));
            }

            base.VisitConditional(operation);
        }

        public override void VisitSimpleAssignment(ISimpleAssignmentOperation operation)
        {
            RecordWrite(operation.Target, operation.Syntax.SpanStart);
            if (Unwrap(operation.Target) is ILocalReferenceOperation local)
            {
                DefineLocal(local.Local, operation.Value, operation.Syntax);
            }
            else
            {
                MarkOpaque(operation.Target, operation.Syntax.SpanStart);
            }

            if (operation.Target is IPropertyReferenceOperation property && IsUserProperty(property.Property))
            {
                _sinks.Add(operation);
            }

            base.VisitSimpleAssignment(operation);
        }

        public override void VisitCoalesceAssignment(ICoalesceAssignmentOperation operation)
        {
            RecordWrite(operation.Target, operation.Syntax.SpanStart);
            if (Unwrap(operation.Target) is ILocalReferenceOperation local)
            {
                DefineLocal(local.Local, operation.Value, operation.Syntax);
            }

            base.VisitCoalesceAssignment(operation);
        }

        private void DefineLocal(ILocalSymbol local, IOperation value, SyntaxNode syntax)
        {
            if (_declared.Contains(local))
            {
                AddDefinition(local, value, syntax);
            }
            else
            {
                parent?.DefineLocal(local, value, syntax);
            }
        }

        public override void VisitCompoundAssignment(ICompoundAssignmentOperation operation)
        {
            MarkOpaqueWrite(operation.Target, operation.Syntax);
            base.VisitCompoundAssignment(operation);
        }

        public override void VisitIncrementOrDecrement(IIncrementOrDecrementOperation operation)
        {
            MarkOpaqueWrite(operation.Target, operation.Syntax);
            base.VisitIncrementOrDecrement(operation);
        }

        public override void VisitDeconstructionAssignment(IDeconstructionAssignmentOperation operation)
        {
            foreach (var target in operation.Target.DescendantsAndSelf())
            {
                MarkOpaqueWrite(target, operation.Syntax);
            }

            base.VisitDeconstructionAssignment(operation);
        }

        public override void VisitArgument(IArgumentOperation operation)
        {
            if (operation.Parameter?.RefKind is RefKind.Ref or RefKind.Out
                && !(operation.Parent is IInvocationOperation invocation && IsClaimReader(invocation)))
            {
                MarkOpaqueWrite(operation.Value, operation.Syntax);
            }

            base.VisitArgument(operation);
        }

        public override void VisitObjectCreation(IObjectCreationOperation operation)
        {
            if (Is(types.Tickets, operation.Type))
            {
                _sinks.Add(operation);
            }

            base.VisitObjectCreation(operation);
        }

        public override void VisitInvocation(IInvocationOperation operation)
        {
            var method = operation.TargetMethod;
            if (IsClaimReader(operation))
            {
                RecordClaimOutput(operation);
            }

            if (string.Equals(method.Name, "ValidateToken", StringComparison.Ordinal)
                && Is(types.JwtHandlers, method.ContainingType)
                && Is(types.Principals, method.ReturnType))
            {
                _validations.Add(operation);
            }

            if (IsAddMethod(method.Name) && StorageSymbol(operation.Instance) is { } target)
            {
                foreach (var argument in operation.Arguments)
                {
                    _added.Add((target, argument.Value, operation.Syntax, this));
                }
            }

            base.VisitInvocation(operation);
        }

        private bool IsClaimReader(IInvocationOperation invocation)
        {
            return types.IsToken(invocation.TargetMethod.ContainingType)
                   && invocation.TargetMethod.Name is "TryGetClaim" or "TryGetPayloadValue" or "TryGetHeaderValue";
        }

        private void RecordClaimOutput(IInvocationOperation invocation)
        {
            foreach (var argument in invocation.Arguments.Where(argument => argument.Parameter?.RefKind == RefKind.Out))
            {
                var value = Unwrap(argument.Value);
                if (value is IDeclarationExpressionOperation declaration)
                {
                    value = declaration.Expression;
                    if (value is ILocalReferenceOperation declared)
                    {
                        _declared.Add(declared.Local);
                    }
                }

                if (value is ILocalReferenceOperation local)
                {
                    RecordWrite(value, invocation.Syntax.SpanStart);
                    DefineLocal(local.Local, invocation, invocation.Syntax);
                }
            }
        }

        internal void Report(OperationBlockAnalysisContext context)
        {
            foreach (var nested in _nestedBodies)
            {
                nested.Report(context);
            }

            foreach (var sink in _sinks)
            {
                var principal = sink switch
                {
                    ISimpleAssignmentOperation assignment => assignment.Value,
                    IObjectCreationOperation creation => Argument(creation.Arguments, "principal"),
                    _ => null
                };
                if (Branches(principal).Any(branch => HasUnvalidatedToken(branch, sink)))
                {
                    var name = sink is ISimpleAssignmentOperation ? "HttpContext.User" : "AuthenticationTicket";
                    context.ReportDiagnostic(Diagnostic.Create(Rule, sink.Syntax.GetLocation(), name));
                }
            }
        }

        private bool HasUnvalidatedToken(IOperation branch, IOperation sink)
        {
            var search = new Search();
            Collect(branch, ValueKind.Principal, sink.Syntax, search);

            // A branch of a conditional is the guard scope, so its own syntax locates the use.
            return search.Tokens.Exists(token => !IsValidated(token, ReadAnchor(token, branch.Syntax)));
        }

        private static IEnumerable<IOperation> Branches(IOperation? value)
        {
            if (value is null)
            {
                return [];
            }

            value = Unwrap(value);
            return value switch
            {
                IConditionalOperation { WhenFalse: { } whenFalse } conditional
                    => Branches(conditional.WhenTrue).Concat(Branches(whenFalse)),
                ICoalesceOperation coalesce => Branches(coalesce.Value).Concat(Branches(coalesce.WhenNull)),
                _ => [value]
            };
        }

        private void Collect(IOperation? value, ValueKind kind, SyntaxNode anchor, Search search)
        {
            if (value is null)
            {
                return;
            }

            value = Unwrap(value);
            if (value is ILocalReferenceOperation local)
            {
                CollectLocal(local.Local, kind, anchor, search);
                return;
            }

            if (kind == ValueKind.Principal)
            {
                CollectPrincipal(value, anchor, search);
            }
            else if (kind == ValueKind.Identity)
            {
                CollectIdentity(value, anchor, search);
            }
            else if (kind == ValueKind.Claims)
            {
                CollectClaims(value, anchor, search);
            }
            else
            {
                CollectToken(value, search);
            }
        }

        private void CollectPrincipal(IOperation value, SyntaxNode anchor, Search search)
        {
            if (value is IObjectCreationOperation creation && Is(types.Principals, creation.Type))
            {
                foreach (var argument in creation.Arguments)
                {
                    Collect(argument.Value, ValueKind.Identity, anchor, search);
                }
            }
        }

        private void CollectIdentity(IOperation value, SyntaxNode anchor, Search search)
        {
            foreach (var node in value.DescendantsAndSelf())
            {
                if (node is IObjectCreationOperation identity && IsAuthenticatedIdentity(identity))
                {
                    Collect(Argument(identity.Arguments, "claims"), ValueKind.Claims, anchor, search);
                }
                else if (node is ILocalReferenceOperation local)
                {
                    CollectLocal(local.Local, ValueKind.Identity, anchor, search);
                }
            }
        }

        private void CollectClaims(IOperation value, SyntaxNode anchor, Search search)
        {
            foreach (var node in value.DescendantsAndSelf())
            {
                if (node is IPropertyReferenceOperation property && types.IsToken(property.Property.ContainingType))
                {
                    Collect(ReceiverOf(property.Instance), ValueKind.Token, anchor, search);
                }
                else if (node is IInvocationOperation invocation && types.IsToken(invocation.TargetMethod.ContainingType))
                {
                    Collect(invocation.Instance, ValueKind.Token, anchor, search);
                }
                else if (node is ILocalReferenceOperation local)
                {
                    CollectLocal(local.Local, ValueKind.Claims, anchor, search);
                }
                else if (node is IParameterReferenceOperation parameter)
                {
                    foreach (var input in LambdaInputs(parameter, search))
                    {
                        Collect(input, ValueKind.Claims, anchor, search);
                    }
                }
            }
        }

        private void CollectToken(IOperation value, Search search)
        {
            var token = value switch
            {
                IInvocationOperation invocation when IsDecoder(invocation.TargetMethod)
                    => Argument(invocation.Arguments, "token"),
                IObjectCreationOperation { Arguments.Length: 1 } creation
                    when types.IsToken(creation.Type) && creation.Arguments[0].Value.Type?.SpecialType == SpecialType.System_String
                    => creation.Arguments[0].Value,
                _ => null
            };
            if (token is not null)
            {
                search.Tokens.Add(token);
            }
        }

        private void CollectLocal(ILocalSymbol local, ValueKind kind, SyntaxNode anchor, Search search)
        {
            if (OpaqueBefore(local, anchor.SpanStart))
            {
                return;
            }

            if (!_declared.Contains(local))
            {
                // A local declared outside a lambda or local function lives in the parent body.
                if (parent is not null && origin is not null)
                {
                    parent.CollectCaptured(local, kind, origin, search);
                }

                return;
            }

            if (!search.Visit(local, kind, anchor.SpanStart))
            {
                return;
            }

            var definitions = LiveDefinitions(local, anchor);
            foreach (var (value, syntax) in definitions)
            {
                Collect(value, kind, syntax, search);
            }

            CollectAdded(local, kind, definitions, anchor, search);
        }

        private void CollectCaptured(ILocalSymbol local, ValueKind kind, SyntaxNode capture, Search search)
        {
            if (!_declared.Contains(local))
            {
                parent?.CollectCaptured(local, kind, capture, search);
                return;
            }

            if (!search.Visit(local, kind, capture.SpanStart))
            {
                return;
            }

            // A local function can run before its declaration. A lambda can run after later writes.
            var definitions = capture is LocalFunctionStatementSyntax ? [] : LiveDefinitions(local, capture);
            if (_definitions.TryGetValue(local, out var all))
            {
                definitions.AddRange(all.Where(definition => capture is LocalFunctionStatementSyntax
                    || definition.Syntax.SpanStart > capture.SpanStart));
            }
            foreach (var (value, syntax) in definitions)
            {
                Collect(value, kind, syntax, search);
            }

            CollectAdded(local, kind, definitions, capture, search);
        }

        private bool SameDefinitions(ILocalSymbol local, List<(IOperation Value, SyntaxNode Syntax)> definitions, SyntaxNode addition)
        {
            return LiveDefinitions(local, addition).Exists(candidate =>
                definitions.Exists(definition => definition.Value == candidate.Value));
        }

        private void CollectAdded(ILocalSymbol local, ValueKind kind, List<(IOperation Value, SyntaxNode Syntax)> definitions, SyntaxNode anchor, Search search)
        {
            var isIdentity = Is(types.Identities, local.Type);
            if (kind == ValueKind.Token
                || (kind == ValueKind.Identity && isIdentity
                    && !definitions.Exists(definition => Unwrap(definition.Value) is IObjectCreationOperation creation
                                                         && IsAuthenticatedIdentity(creation))))
            {
                return;
            }

            // An identity is stored by reference, so a claim added after the sink still reaches the request.
            var addedKind = kind == ValueKind.Principal || (kind == ValueKind.Identity && !isIdentity)
                ? ValueKind.Identity
                : ValueKind.Claims;
            // An identity copies a claim collection when it is built, so only an earlier add reaches it.
            var copied = kind == ValueKind.Claims && !isIdentity;
            foreach (var (target, value, syntax, owner) in _added)
            {
                if (SymbolEqualityComparer.Default.Equals(target, local)
                    && (!copied || syntax.SpanStart < anchor.SpanStart)
                    && SameDefinitions(local, definitions, syntax))
                {
                    owner.Collect(value, addedKind, syntax, search);
                }
            }
        }

        /// <summary>
        ///     Returns the definitions of <paramref name="local" /> that can reach <paramref name="anchor" />.
        ///     A definition in a block that encloses the anchor hides every earlier definition.
        /// </summary>
        private List<(IOperation Value, SyntaxNode Syntax)> LiveDefinitions(ILocalSymbol local, SyntaxNode anchor)
        {
            if (!_definitions.TryGetValue(local, out var all))
            {
                return [];
            }

            var before = all.FindAll(definition => definition.Syntax.SpanStart < anchor.SpanStart
                // A pattern variable is scoped to its own arm, so it is live wherever its input is read.
                || definition.Syntax is CasePatternSwitchLabelSyntax or SwitchExpressionArmSyntax or IsPatternExpressionSyntax);
            // The walker visits a for incrementor after the body, so order by source position.
            before.Sort((left, right) => left.Syntax.SpanStart.CompareTo(right.Syntax.SpanStart));
            var start = before.FindLastIndex(definition =>
                definition.Syntax.SpanStart < anchor.SpanStart && Kills(definition.Syntax, anchor));
            var live = start <= 0 ? before : before.GetRange(start, before.Count - start);
            var killer = start < 0 ? null : before[start].Syntax;
            // A definition after the anchor in a shared loop reaches the anchor on the next iteration.
            live.AddRange(all.Where(definition => !before.Contains(definition)
                                                  && ReachesByLoop(local, definition.Syntax, anchor, killer, all)));
            return live;
        }

        /// <summary>
        ///     Reports whether <paramref name="definition" /> replaces every earlier value at <paramref name="point" />:
        ///     a declarator, or a top-level assignment statement in a block that contains the point.
        /// </summary>
        private static bool Kills(SyntaxNode definition, SyntaxNode point)
        {
            return definition.FirstAncestorOrSelf<StatementSyntax>() is { Parent: BlockSyntax block } statement
                   && block.Span.Contains(point.Span)
                   && (definition is VariableDeclaratorSyntax
                       || definition.RawKind == (int)Microsoft.CodeAnalysis.CSharp.SyntaxKind.SimpleAssignmentExpression
                          && statement is ExpressionStatementSyntax expression && expression.Expression == definition);
        }

        private static bool IsLoop(SyntaxNode node)
        {
            return node is WhileStatementSyntax or DoStatementSyntax or ForStatementSyntax or ForEachStatementSyntax;
        }

        private static bool ReachesByLoop(
            ILocalSymbol local,
            SyntaxNode definition,
            SyntaxNode anchor,
            SyntaxNode? killer,
            List<(IOperation Value, SyntaxNode Syntax)> all)
        {
            var loop = anchor.Ancestors().FirstOrDefault(node => IsLoop(node) && node.Span.Contains(definition.Span));

            // A local declared in the loop is unassigned again on each iteration.
            return loop is not null
                   && (killer is null || !loop.Span.Contains(killer.Span))
                   && !local.DeclaringSyntaxReferences.Any(reference => loop.Span.Contains(reference.Span))
                   && !all.Exists(other => other.Syntax.SpanStart > definition.SpanStart
                                           && loop.Span.Contains(other.Syntax.Span)
                                           && Kills(other.Syntax, definition));
        }

        /// <summary>
        ///     Reports whether a loop that starts after the validation rewrites the token after the use,
        ///     so the next iteration reads an unvalidated value.
        /// </summary>
        private bool RewrittenInLoop(IOperation token, SyntaxNode use, int validationStart)
        {
            foreach (var node in use.Ancestors())
            {
                if (node is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax)
                {
                    return false;
                }

                if (IsLoop(node) && node.SpanStart > validationStart && WrittenBetween(token, use.SpanStart, node.Span.End))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        ///     A decoded token read after the sink in a shared loop reaches the sink on the next iteration,
        ///     so its own read site is the use that a validation must precede.
        /// </summary>
        private static SyntaxNode ReadAnchor(IOperation token, SyntaxNode use)
        {
            var read = token.Syntax;
            if (read.SpanStart <= use.SpanStart)
            {
                return use;
            }

            var loop = use.Ancestors()
                .TakeWhile(node => node is not (AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax))
                .FirstOrDefault(node => IsLoop(node) && node.Span.Contains(read.Span));
            return loop is not null
                   && !read.Ancestors().TakeWhile(node => node != loop)
                       .Any(node => node is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax)
                ? read
                : use;
        }

        private static IEnumerable<IOperation?> LambdaInputs(IParameterReferenceOperation reference, Search search)
        {
            var lambda = reference.Parent;
            while (lambda is not null
                   && !(lambda is IAnonymousFunctionOperation function
                        && SymbolEqualityComparer.Default.Equals(function.Symbol, reference.Parameter.ContainingSymbol)))
            {
                lambda = lambda.Parent;
            }

            return lambda?.Parent is IDelegateCreationOperation { Parent: IArgumentOperation { Parent: IInvocationOperation invocation } argument }
                   && search.VisitLambda(lambda)
                ? invocation.Arguments.Where(other => other != argument).Select(other => other.Value).Prepend(invocation.Instance)
                : [];
        }

        private bool IsValidated(IOperation token, SyntaxNode sink)
        {
            if (StorageSymbol(token) is null)
            {
                return false;
            }

            if (IsGuarded(token, sink) || ValidatedBefore(token, sink))
            {
                return true;
            }

            // A lambda runs after its creation, so a parent validation before the lambda covers it.
            // A local function can run before its declaration, so only a parent guard that scopes it counts.
            return parent is not null && origin is not null
                   && !WrittenBetween(token, origin.SpanStart, sink.SpanStart)
                   && !parent.WrittenBetween(token, origin.SpanStart, int.MaxValue)
                   && (origin is LocalFunctionStatementSyntax
                       ? parent.IsGuarded(token, origin)
                       : parent.IsValidated(token, origin));
        }

        private bool IsGuarded(IOperation token, SyntaxNode sink)
        {
            foreach (var (scope, result) in _passGuards)
            {
                if (scope.Span.Contains(sink.Span)
                    && !WrittenBetween(token, scope.SpanStart, sink.SpanStart)
                    && !RewrittenInLoop(token, sink, scope.SpanStart)
                    && ResultValidates(result, token, sink))
                {
                    return true;
                }
            }

            return false;
        }

        private bool ValidatedBefore(IOperation token, SyntaxNode sink)
        {
            var boundary = sink.Ancestors().FirstOrDefault(node => node is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax);
            for (var statement = sink.FirstAncestorOrSelf<StatementSyntax>();
                 statement is not null && (boundary is null || (statement != boundary && boundary.Span.Contains(statement.Span)));
                 statement = statement.Parent?.FirstAncestorOrSelf<StatementSyntax>())
            {
                if (statement.Parent is not BlockSyntax block)
                {
                    continue;
                }

                if (block.DescendantNodes(node => node is not AnonymousFunctionExpressionSyntax and not LocalFunctionStatementSyntax)
                    .Any(node => node is LabeledStatementSyntax or GotoStatementSyntax))
                {
                    return false;
                }

                foreach (var previous in block.Statements.TakeWhile(candidate => candidate != statement))
                {
                    if (previous is LabeledStatementSyntax or GotoStatementSyntax)
                    {
                        return false;
                    }

                    if (ValidatesToken(previous, token)
                        && !WrittenBetween(token, previous.Span.End, sink.SpanStart)
                        && !RewrittenInLoop(token, sink, previous.SpanStart))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool ValidatesToken(StatementSyntax statement, IOperation token)
        {
            if (statement is TryStatementSyntax guarded)
            {
                return guarded.Catches.All(handler => SyntaxExits(handler.Block))
                       && guarded.Block.Statements.Any(previous => ValidatesToken(previous, token)
                           && !WrittenBetween(token, previous.Span.End, guarded.Span.End))
                       && !guarded.Block.DescendantNodes().Any(node => node is GotoStatementSyntax or LabeledStatementSyntax);
            }

            var expression = statement switch
            {
                ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax { RawKind: (int)Microsoft.CodeAnalysis.CSharp.SyntaxKind.SimpleAssignmentExpression } assignment } => assignment.Right,
                ExpressionStatementSyntax plain => plain.Expression,
                LocalDeclarationStatementSyntax { Declaration.Variables.Count: 1 } declaration
                    => declaration.Declaration.Variables[0].Initializer?.Value,
                _ => null
            };
            foreach (var validation in _validations)
            {
                if (expression == validation.Syntax
                    && ValidatesSymbol(Argument(validation.Arguments, "token"), token, statement))
                {
                    return true;
                }
            }

            foreach (var (guard, result) in _guards)
            {
                if (guard == statement && ResultValidates(result, token, statement))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        ///     Reports whether a validated argument stands for <paramref name="token" />. A decoded token
        ///     argument stands for the string it was decoded from.
        /// </summary>
        private bool ValidatesSymbol(IOperation? argument, IOperation token, SyntaxNode anchor)
        {
            if (argument is null)
            {
                return false;
            }

            if (SameStorage(argument, token))
            {
                return true;
            }

            var search = new Search();
            Collect(argument, ValueKind.Token, anchor, search);
            return search.Tokens.Exists(source => SameStorage(source, token));
        }

        private bool ResultValidates(ILocalSymbol result, IOperation token, SyntaxNode use)
        {
            return !_declared.Contains(result)
                ? parent?.ResultValidates(result, token, use) == true
                : _definitions.TryGetValue(result, out var definitions)
                  && definitions.Count == 1
                  && !WrittenBetween(token, definitions[0].Syntax.SpanStart, use.SpanStart)
                  && ValidatesSymbol(ValidatedToken(result), token, definitions[0].Syntax);
        }

        private IOperation? ValidatedToken(ILocalSymbol result)
        {
            if (OpaqueBefore(result, int.MaxValue))
            {
                return null;
            }

            if (!_declared.Contains(result))
            {
                return parent?.ValidatedToken(result);
            }

            if (!_definitions.TryGetValue(result, out var definitions) || definitions.Count != 1)
            {
                return null;
            }

            var call = Unwrap(definitions[0].Value);
            if (call is IAwaitOperation awaited)
            {
                call = Unwrap(awaited.Operation);
            }

            if (call is IInvocationOperation { TargetMethod.Name: "ConfigureAwait", Instance: { } task })
            {
                call = Unwrap(task);
            }

            return call is IInvocationOperation invocation
                   && types.IsHandler(invocation.TargetMethod.ContainingType)
                   && (string.Equals(invocation.TargetMethod.Name, "ValidateTokenAsync", StringComparison.Ordinal)
                       || string.Equals(invocation.TargetMethod.Name, "ValidateToken", StringComparison.Ordinal)
                          && Is(types.JsonHandlers, invocation.TargetMethod.ContainingType))
                ? Argument(invocation.Arguments, "token")
                : null;
        }

        private bool WrittenBetween(IOperation token, int start, int end)
        {
            return token.DescendantsAndSelf().Any(node => StorageSymbol(node) is { } symbol && WrittenBetween(symbol, start, end));
        }

        private bool WrittenBetween(ISymbol symbol, int start, int end)
        {
            return _writes.TryGetValue(symbol, out var positions) && positions.Exists(position => position > start && position < end);
        }

        private bool IsDecoder(IMethodSymbol method)
        {
            return method.Name switch
            {
                "ReadJwtToken" => Is(types.JwtHandlers, method.ContainingType),
                "ReadJsonWebToken" => Is(types.JsonHandlers, method.ContainingType),
                "ReadToken" => types.IsHandler(method.ContainingType),
                _ => false
            };
        }

        private static bool SyntaxExits(BlockSyntax block)
        {
            return block.Statements.LastOrDefault() is ReturnStatementSyntax or ThrowStatementSyntax;
        }

        private bool IsAuthenticatedIdentity(IObjectCreationOperation identity)
        {
            return AuthenticatedIdentity(identity, new HashSet<ILocalSymbol>(SymbolEqualityComparer.Default));
        }

        private bool AuthenticatedIdentity(IOperation value, HashSet<ILocalSymbol> visited)
        {
            value = Unwrap(value);
            if (value is ILocalReferenceOperation local && visited.Add(local.Local))
            {
                return _definitions.TryGetValue(local.Local, out var definitions)
                    ? definitions.Exists(definition => AuthenticatedIdentity(definition.Value, visited))
                    : parent?.AuthenticatedIdentity(value, visited) == true;
            }

            if (value is not IObjectCreationOperation creation || !Is(types.Identities, creation.Type))
            {
                return false;
            }

            var type = Argument(creation.Arguments, "authenticationType") ?? Argument(creation.Arguments, "type");
            return type is not null
                ? type.ConstantValue is not { HasValue: true } constant || constant.Value is string { Length: > 0 }
                : Argument(creation.Arguments, "identity") is { } inherited && AuthenticatedIdentity(inherited, visited);
        }

        private bool IsUserProperty(IPropertySymbol property)
        {
            if (!string.Equals(property.Name, "User", StringComparison.Ordinal))
            {
                return false;
            }

            for (var current = property; current is not null; current = current.OverriddenProperty)
            {
                if (Is(types.HttpContexts, current.ContainingType))
                {
                    return true;
                }
            }

            return false;
        }

        private void VisitNested(IOperation? body, SyntaxNode syntax)
        {
            var nested = new BodyAnalysis(types, localFunctions, this, syntax);
            nested.Visit(body);
            _nestedBodies.Add(nested);
            MergePositions(_opaque, nested._opaque);
            _added.AddRange(nested._added);
            MergePositions(_writes, nested._writes);
        }

        private void AddDefinition(ILocalSymbol local, IOperation value, SyntaxNode syntax)
        {
            if (!_definitions.TryGetValue(local, out var definitions))
            {
                definitions = [];
                _definitions[local] = definitions;
            }

            definitions.Add((value, syntax));
        }

        private void RecordWrite(IOperation target, int position)
        {
            if (StorageSymbol(target) is { } symbol)
            {
                AddWrite(symbol, position);
            }
        }

        private void AddWrite(ISymbol symbol, int position)
        {
            AddPosition(_writes, symbol, position);
        }

        private static void AddPosition(Dictionary<ISymbol, List<int>> writes, ISymbol symbol, int position)
        {
            if (!writes.TryGetValue(symbol, out var positions))
            {
                positions = [];
                writes[symbol] = positions;
            }

            positions.Add(position);
        }

        private static void MergePositions(Dictionary<ISymbol, List<int>> target, Dictionary<ISymbol, List<int>> source)
        {
            foreach (var pair in source)
            {
                foreach (var position in pair.Value)
                {
                    AddPosition(target, pair.Key, position);
                }
            }
        }

        private bool OpaqueBefore(ISymbol symbol, int position)
        {
            return _opaque.TryGetValue(symbol, out var positions) && positions.Exists(write => write < position);
        }

        private void MarkOpaque(IOperation target, int position)
        {
            if (StorageSymbol(target) is { } symbol)
            {
                AddPosition(_opaque, symbol, position);
            }
        }

        private void MarkOpaqueWrite(IOperation target, SyntaxNode syntax)
        {
            RecordWrite(target, syntax.SpanStart);
            MarkOpaque(target, syntax.SpanStart);
        }

        private static IOperation? ReceiverOf(IOperation? instance)
        {
            if (instance is not IConditionalAccessInstanceOperation)
            {
                return instance;
            }

            var current = instance.Parent;
            while (current is not null and not IConditionalAccessOperation)
            {
                current = current.Parent;
            }

            return (current as IConditionalAccessOperation)?.Operation;
        }

        private static bool IsAddMethod(string name)
        {
            return name is "AddClaims" or "AddClaim" or "AddIdentity" or "AddIdentities" or "Add" or "AddRange";
        }

        private static IEnumerable<ILocalSymbol> PassedResults(IOperation condition)
        {
            condition = Unwrap(condition);
            if (condition is IBinaryOperation { OperatorKind: BinaryOperatorKind.ConditionalAnd } both)
            {
                return PassedResults(both.LeftOperand).Concat(PassedResults(both.RightOperand));
            }

            var passed = condition is IBinaryOperation
            {
                OperatorKind: BinaryOperatorKind.Equals,
                RightOperand.ConstantValue: { HasValue: true, Value: true }
            } equals
                ? equals.LeftOperand
                : condition;
            return ValidResult(passed) is { } result ? [result] : [];
        }

        private static ILocalSymbol? ValidResult(IOperation operation)
        {
            return operation is IPropertyReferenceOperation { Instance: ILocalReferenceOperation local } property
                   && string.Equals(property.Property.Name, "IsValid", StringComparison.Ordinal)
                ? local.Local
                : null;
        }

        private static ILocalSymbol? FailedResult(IOperation condition)
        {
            var failed = Unwrap(condition) switch
            {
                IUnaryOperation { OperatorKind: UnaryOperatorKind.Not } not => not.Operand,
                IBinaryOperation
                {
                    OperatorKind: BinaryOperatorKind.Equals,
                    RightOperand.ConstantValue: { HasValue: true, Value: false }
                } equals => equals.LeftOperand,
                _ => null
            };
            return failed is null ? null : ValidResult(failed);
        }

        private static bool Exits(IOperation operation)
        {
            return operation switch
            {
                IReturnOperation { Kind: OperationKind.Return or OperationKind.YieldBreak } => true,
                IThrowOperation or IExpressionStatementOperation { Operation: IThrowOperation } => true,
                IBlockOperation { Operations.Length: > 0 } block => Exits(block.Operations[block.Operations.Length - 1]),
                _ => false
            };
        }

        private static IOperation? Argument(ImmutableArray<IArgumentOperation> arguments, string name)
        {
            foreach (var argument in arguments)
            {
                if (string.Equals(argument.Parameter?.Name, name, StringComparison.Ordinal))
                {
                    return argument.Value;
                }
            }

            return null;
        }

        private static ISymbol? StorageSymbol(IOperation? operation)
        {
            return operation is null ? null : Unwrap(operation) switch
            {
                ILocalReferenceOperation local => local.Local,
                IParameterReferenceOperation parameter => parameter.Parameter,
                IFieldReferenceOperation field => field.Field,
                IPropertyReferenceOperation property when IsStableProperty(property.Property) => property.Property,
                _ => null
            };
        }

        private static bool IsStableProperty(IPropertySymbol property)
        {
            return !property.IsIndexer && property.DeclaringSyntaxReferences.Any(reference =>
                reference.GetSyntax() is PropertyDeclarationSyntax { ExpressionBody: null } declaration
                && declaration.AccessorList?.Accessors.Any(accessor =>
                    accessor.RawKind == (int)Microsoft.CodeAnalysis.CSharp.SyntaxKind.GetAccessorDeclaration
                    && accessor.Body is null && accessor.ExpressionBody is null) == true);
        }

        private static bool SameStorage(IOperation? left, IOperation? right)
        {
            if (left is null || right is null)
            {
                return left is null && right is null;
            }

            left = Unwrap(left);
            right = Unwrap(right);
            return left is IInstanceReferenceOperation leftThis && right is IInstanceReferenceOperation rightThis
                ? leftThis.ReferenceKind == rightThis.ReferenceKind
                  && SymbolEqualityComparer.Default.Equals(leftThis.Type, rightThis.Type)
                : StorageSymbol(left) is { } symbol
                  && SymbolEqualityComparer.Default.Equals(symbol, StorageSymbol(right))
                  && SameStorage(StorageReceiver(left), StorageReceiver(right));
        }

        private static IOperation? StorageReceiver(IOperation operation)
        {
            return operation switch
            {
                IFieldReferenceOperation field => field.Instance,
                IPropertyReferenceOperation property => property.Instance,
                _ => null
            };
        }

        private static IOperation Unwrap(IOperation operation)
        {
            while (operation is IConversionOperation { OperatorMethod: null } conversion)
            {
                operation = conversion.Operand;
            }

            return operation;
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
